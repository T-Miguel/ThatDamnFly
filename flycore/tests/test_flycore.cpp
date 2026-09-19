#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "doctest.h"
#include "flycore.h"
#include "model_builder.h"
#include "sha256.h"
#include <cmath>
#include <cstring>
#include <string>
#include <utility>
#include <vector>

static fc_sensory_frame zeroFrame() { fc_sensory_frame f{}; f.struct_size = sizeof(f); f.version = FC_ABI_VERSION; f.background_drive = 0.f; return f; }
static fc_motor_frame outFrame() { fc_motor_frame m{}; m.struct_size = sizeof(m); m.version = FC_ABI_VERSION; return m; }

// Minimal model: 0 = "sensor" (receives the expansion channel of sector 0), 1 = "DN" (receives from 0), output channel 0 reads 1.
static ModelSpec twoNeuron(float w = 20.f, uint16_t d = 2) {
    ModelSpec s; s.model_id = "two"; s.neurons.resize(2);
    s.edges.push_back({0, 1, w, d});
    s.inputs.push_back({0 /*expansion, sector 0*/, 0, 1.0f /*prob=1 when x>=1*/, 20.f});
    s.outputs.push_back({0, 1, 1.f});
    return s;
}

TEST_CASE("sha256 known vectors") {
    uint8_t d[32]; fc_sha256((const uint8_t*)"abc", 3, d);
    const uint8_t exp[32] = {0xba,0x78,0x16,0xbf,0x8f,0x01,0xcf,0xea,0x41,0x41,0x40,0xde,0x5d,0xae,0x22,0x23,0xb0,0x03,0x61,0xa3,0x96,0x17,0x7a,0x9c,0xb4,0x10,0xff,0x61,0xf2,0x00,0x15,0xad};
    CHECK(std::memcmp(d, exp, 32) == 0);
    uint8_t e[32]; fc_sha256((const uint8_t*)"", 0, e);
    const uint8_t exp0[32] = {0xe3,0xb0,0xc4,0x42,0x98,0xfc,0x1c,0x14,0x9a,0xfb,0xf4,0xc8,0x99,0x6f,0xb9,0x24,0x27,0xae,0x41,0xe4,0x64,0x9b,0x93,0x4c,0xa4,0x95,0x99,0x1b,0x78,0x52,0xb8,0x55};
    CHECK(std::memcmp(e, exp0, 32) == 0);
    // padding boundaries (55/56/64 bytes); vectors computed with hashlib
    auto hex = [](const uint8_t* d) { static const char* H = "0123456789abcdef"; std::string r; for (int i = 0; i < 32; ++i) { r += H[d[i] >> 4]; r += H[d[i] & 15]; } return r; };
    auto sha_of = [&](size_t len) { std::string in(len, 'a'); uint8_t o[32]; fc_sha256((const uint8_t*)in.data(), len, o); return hex(o); };
    CHECK(sha_of(55) == "9f4390f8d30c2dd92ec9f095b65e2b9ae9b0a925a5258e241c9f1e910f734318");
    CHECK(sha_of(56) == "b35439a4ac6f0948b6d6f9e3c6af0f5f590ce20f1bde7090ef7970686ec6738a");
    CHECK(sha_of(64) == "ffe054fe7ae0cb6dc65c3af9b61d5209f439851db43d0ba5997337df154668eb");
}

TEST_CASE("inspect and create accept a valid model; reject corruption") {
    auto bytes = buildModel(twoNeuron());
    fc_model_info info{}; info.struct_size = sizeof(info);
    REQUIRE(fc_inspect(bytes.data(), bytes.size(), &info) == FC_OK);
    CHECK(info.neuron_count == 2); CHECK(info.edge_count == 1); CHECK(std::string(info.model_id) == "two");
    SUBCASE("hash mismatch") { auto b = bytes; b[200] ^= 0x01; CHECK(fc_inspect(b.data(), b.size(), &info) == FC_E_HASH_MISMATCH); }
    SUBCASE("bad magic") { auto b = bytes; b[0] = 'X'; CHECK(fc_inspect(b.data(), b.size(), &info) == FC_E_BAD_MODEL); }
    SUBCASE("truncated") { CHECK(fc_inspect(bytes.data(), bytes.size() - 5, &info) == FC_E_BAD_MODEL); }
    SUBCASE("post index out of range") { ModelSpec s = twoNeuron(); s.edges[0].post = 7; auto b = buildModel(s); CHECK(fc_inspect(b.data(), b.size(), &info) == FC_E_BAD_MODEL); }
    SUBCASE("delay zero") { ModelSpec s = twoNeuron(); s.edges[0].delay = 0; auto b = buildModel(s); CHECK(fc_inspect(b.data(), b.size(), &info) == FC_E_BAD_MODEL); }
    SUBCASE("delay above max") { ModelSpec s = twoNeuron(); s.edges[0].delay = 9; auto b = buildModel(s); CHECK(fc_inspect(b.data(), b.size(), &info) == FC_E_BAD_MODEL); }
    SUBCASE("abi struct size") { fc_model_info bad{}; bad.struct_size = 4; CHECK(fc_inspect(bytes.data(), bytes.size(), &bad) == FC_E_ABI); }
}

TEST_CASE("stimulus propagates through the edge") {
    auto bytes = buildModel(twoNeuron(20.f, 3));
    fc_handle* h = nullptr; REQUIRE(fc_create(bytes.data(), bytes.size(), 42, &h) == FC_OK);
    auto in = zeroFrame(); auto out = outFrame();
    for (int i = 0; i < 10; ++i) REQUIRE(fc_step(h, &in, 10, &out) == FC_OK);
    CHECK(out.spikes_this_step == 0); CHECK(out.channel[0] == doctest::Approx(0.f));
    in.expansion[0] = 1.f;
    for (int i = 0; i < 5; ++i) REQUIRE(fc_step(h, &in, 10, &out) == FC_OK);
    CHECK(out.spikes_this_step > 0);
    uint32_t counts[2]; REQUIRE(fc_read_spike_counts(h, counts, 2) == FC_OK);
    CHECK(counts[0] > 0); CHECK(counts[1] > 0);
    CHECK(out.channel[0] > 0.f);
    fc_destroy(h);
}

TEST_CASE("delay is honoured to the millisecond") {
    // edge with 5 ms delay and a strong weight: the post spikes exactly 5 ms after the pre.
    ModelSpec s = twoNeuron(30.f, 5); s.neurons[0].tau_m = 1.f; s.neurons[1].tau_m = 1.f; // fast membranes: spike in the ms of the event
    auto bytes = buildModel(s);
    fc_handle* h = nullptr; REQUIRE(fc_create(bytes.data(), bytes.size(), 1, &h) == FC_OK);
    auto in = zeroFrame(); in.expansion[0] = 1.f; auto out = outFrame();
    // pre receives an event at ms 1 (prob 1, +20 mV) and spikes in that ms; post receives it 5 ms later
    std::vector<uint32_t> post_hist;
    for (int ms = 1; ms <= 8; ++ms) { fc_step(h, &in, 1, &out); uint32_t c[2]; fc_read_spike_counts(h, c, 2); post_hist.push_back(c[1]); in.expansion[0] = 0.f; }
    // pre spikes at ms 1; the event arrives at ms 6 (delay 5) and the post spikes in that ms
    CHECK(post_hist[4] == 0); CHECK(post_hist[5] == 1);
    fc_destroy(h);
}

TEST_CASE("edge mask silences the pathway (ablation for causal trials)") {
    auto bytes = buildModel(twoNeuron());
    fc_handle* h = nullptr; REQUIRE(fc_create(bytes.data(), bytes.size(), 7, &h) == FC_OK);
    uint8_t mask[1] = {1}; REQUIRE(fc_set_edge_mask(h, mask, 1) == FC_OK);
    auto in = zeroFrame(); in.expansion[0] = 1.f; auto out = outFrame();
    for (int i = 0; i < 20; ++i) REQUIRE(fc_step(h, &in, 10, &out) == FC_OK);
    uint32_t counts[2]; fc_read_spike_counts(h, counts, 2);
    CHECK(counts[0] > 0); CHECK(counts[1] == 0); CHECK(out.channel[0] == doctest::Approx(0.f));
    REQUIRE(fc_clear_edge_mask(h) == FC_OK); fc_clear_spike_counts(h);
    for (int i = 0; i < 20; ++i) fc_step(h, &in, 10, &out);
    fc_read_spike_counts(h, counts, 2); CHECK(counts[1] > 0);
    fc_destroy(h);
}

TEST_CASE("determinism: same seed and inputs give identical outputs; different seed differs") {
    ModelSpec s = twoNeuron(); s.inputs[0].rate_per_ms = 0.3f; // stochastic
    auto bytes = buildModel(s);
    auto run = [&](uint64_t seed) {
        fc_handle* h; REQUIRE(fc_create(bytes.data(), bytes.size(), seed, &h) == FC_OK);
        auto in = zeroFrame(); in.expansion[0] = 1.f; auto out = outFrame(); uint32_t total = 0;
        for (int i = 0; i < 50; ++i) { fc_step(h, &in, 10, &out); total += out.spikes_this_step; }
        float a = out.channel[0]; fc_destroy(h); return std::make_pair(total, a);
    };
    auto a = run(1), b = run(1), c = run(2);
    CHECK(a == b); CHECK(a != c);
}

TEST_CASE("snapshot/restore reproduces the trajectory") {
    ModelSpec s = twoNeuron(); s.inputs[0].rate_per_ms = 0.3f; auto bytes = buildModel(s);
    fc_handle* h; REQUIRE(fc_create(bytes.data(), bytes.size(), 5, &h) == FC_OK);
    auto in = zeroFrame(); in.expansion[0] = 1.f; auto out = outFrame();
    for (int i = 0; i < 7; ++i) fc_step(h, &in, 10, &out);
    size_t len = 0; REQUIRE(fc_snapshot(h, nullptr, &len) == FC_OK); std::vector<uint8_t> snap(len); REQUIRE(fc_snapshot(h, snap.data(), &len) == FC_OK);
    uint32_t t1 = 0; for (int i = 0; i < 5; ++i) { fc_step(h, &in, 10, &out); t1 += out.spikes_this_step; } float a1 = out.channel[0];
    REQUIRE(fc_restore(h, snap.data(), snap.size()) == FC_OK);
    uint32_t t2 = 0; for (int i = 0; i < 5; ++i) { fc_step(h, &in, 10, &out); t2 += out.spikes_this_step; } float a2 = out.channel[0];
    CHECK(t1 == t2); CHECK(a1 == doctest::Approx(a2));
    fc_destroy(h);
}

TEST_CASE("reset restores initial state") {
    auto bytes = buildModel(twoNeuron());
    fc_handle* h; REQUIRE(fc_create(bytes.data(), bytes.size(), 5, &h) == FC_OK);
    auto in = zeroFrame(); in.expansion[0] = 1.f; auto out = outFrame();
    for (int i = 0; i < 5; ++i) fc_step(h, &in, 10, &out);
    REQUIRE(fc_reset(h, 5) == FC_OK);
    uint32_t counts[2]; fc_read_spike_counts(h, counts, 2); CHECK(counts[0] == 0);
    in.expansion[0] = 0.f; fc_step(h, &in, 10, &out); CHECK(out.channel[0] == doctest::Approx(0.f));
    fc_destroy(h);
}

TEST_CASE("invalid arguments are rejected") {
    auto bytes = buildModel(twoNeuron());
    fc_handle* h; REQUIRE(fc_create(bytes.data(), bytes.size(), 1, &h) == FC_OK);
    auto in = zeroFrame(); auto out = outFrame();
    CHECK(fc_step(nullptr, &in, 10, &out) == FC_E_INVALID_ARG);
    CHECK(fc_step(h, &in, 0, &out) == FC_E_INVALID_ARG);
    in.struct_size = 1; CHECK(fc_step(h, &in, 10, &out) == FC_E_ABI); in = zeroFrame();
    in.expansion[0] = std::nanf(""); CHECK(fc_step(h, &in, 10, &out) == FC_E_INVALID_ARG);
    uint8_t m[3] = {0,0,0}; CHECK(fc_set_edge_mask(h, m, 3) == FC_E_INVALID_ARG);
    fc_destroy(h); fc_destroy(nullptr);
}
