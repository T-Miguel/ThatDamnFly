// Benchmark of the cost per tick (10 ms) for a synthetic model with N neurons and mean degree K.
// Not an acceptance test; it serves to size the circuit before measuring on real devices.
#include "flycore.h"
#include "model_builder.h"
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <random>

int main(int argc, char** argv) {
    uint32_t n = argc > 1 ? (uint32_t)atoi(argv[1]) : 2000;
    uint32_t k = argc > 2 ? (uint32_t)atoi(argv[2]) : 50;
    int ticks = argc > 3 ? atoi(argv[3]) : 3000;
    ModelSpec s; s.model_id = "bench"; s.neurons.resize(n);
    std::mt19937 rng(1); std::uniform_int_distribution<uint32_t> pick(0, n - 1); std::uniform_real_distribution<float> w(-1.f, 3.f);
    s.edges.reserve((size_t)n * k);
    for (uint32_t i = 0; i < n; ++i) for (uint32_t j = 0; j < k; ++j) s.edges.push_back({i, pick(rng), w(rng), (uint16_t)(1 + (rng() % 4))});
    for (uint32_t i = 0; i < n; ++i) { s.inputs.push_back({40u, i, 0.02f, 3.f}); if (i % 8 == 0) s.inputs.push_back({(uint32_t)(i % 8), i, 0.5f, 6.f}); }
    for (uint32_t i = 0; i < 16; ++i) s.outputs.push_back({i % 8, i, 1.f});
    auto bytes = buildModel(s);
    fc_handle* h; if (fc_create(bytes.data(), bytes.size(), 1, &h) != FC_OK) { std::puts("create failed"); return 1; }
    fc_sensory_frame in{}; in.struct_size = sizeof(in); in.version = FC_ABI_VERSION; in.background_drive = 1.f; in.expansion[2] = 0.5f;
    fc_motor_frame out{}; out.struct_size = sizeof(out); out.version = FC_ABI_VERSION;
    uint64_t spikes = 0; auto t0 = std::chrono::steady_clock::now();
    for (int t = 0; t < ticks; ++t) { if (fc_step(h, &in, 10, &out) != FC_OK) { std::puts("step failed"); return 1; } spikes += out.spikes_this_step; }
    auto dt = std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - t0).count();
    std::printf("N=%u E=%zu ticks=%d total=%.1f ms per_tick=%.4f ms spikes/tick=%.1f model_bytes=%zu\n", n, s.edges.size(), ticks, dt, dt / ticks, (double)spikes / ticks, bytes.size());
    fc_destroy(h); return 0;
}
