// FlyCore - C++17 implementation of the sparse LIF simulator. See include/flycore.h and docs_FORMAT.md.
// Rules: no allocations on the hot path, no exceptions crossing the ABI, no I/O, no callbacks.
#define FC_BUILD 1
#include "flycore.h"
#include "sha256.h"

#include <cstring>
#include <cmath>
#include <vector>
#include <new>
#include <algorithm>

namespace {

constexpr uint32_t kFormatVersion = 1;
constexpr uint32_t kHeaderSize = 144;
constexpr uint32_t kInputChannels = 5 * FC_SECTORS + 1;   // 41
constexpr uint32_t kMaxNeurons = 20000;                   // compiled limits (M0)
constexpr uint32_t kMaxEdges = 2000000;
constexpr uint32_t kMaxDelay = 64;
constexpr uint32_t kMaxInputEntries = 400000;
constexpr uint32_t kMaxOutputEntries = 20000;

// ---------- RNG: splitmix64 + xoshiro256** ----------
struct Rng {
    uint64_t s[4];
    static uint64_t splitmix(uint64_t& x) {
        uint64_t z = (x += 0x9e3779b97f4a7c15ULL);
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9ULL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebULL;
        return z ^ (z >> 31);
    }
    void seed(uint64_t seed) { uint64_t x = seed; for (auto& v : s) v = splitmix(x); }
    static inline uint64_t rotl(uint64_t x, int k) { return (x << k) | (x >> (64 - k)); }
    inline uint64_t next() {
        const uint64_t result = rotl(s[1] * 5, 7) * 9;
        const uint64_t t = s[1] << 17;
        s[2] ^= s[0]; s[3] ^= s[1]; s[1] ^= s[2]; s[0] ^= s[3]; s[2] ^= t; s[3] = rotl(s[3], 45);
        return result;
    }
    inline float uniform() { return (float)(next() >> 40) * (1.0f / 16777216.0f); } // 24 bits
};

struct Reader {
    const uint8_t* p; size_t len; size_t off = 0; bool ok = true;
    Reader(const uint8_t* p_, size_t len_) : p(p_), len(len_) {}
    bool need(size_t n) { if (!ok || n > len - off) { ok = false; return false; } return true; }
    template <class T> const T* take(size_t count) {
        size_t bytes = count * sizeof(T);
        if (count != 0 && bytes / count != sizeof(T)) { ok = false; return nullptr; }
        if (!need(bytes)) return nullptr;
        const T* r = reinterpret_cast<const T*>(p + off); off += bytes; return r;
    }
    uint32_t u32() { const uint32_t* v = take<uint32_t>(1); return v ? readU32(v) : 0; }
    uint64_t u64() { const uint64_t* v = take<uint64_t>(1); uint64_t r = 0; if (v) std::memcpy(&r, v, 8); return r; }
    static uint32_t readU32(const void* q) { uint32_t r; std::memcpy(&r, q, 4); return r; }
};

struct Header {
    uint32_t format_version, header_size, n, e, m_in, m_out, max_delay, flags;
    uint64_t payload_len; uint8_t sha[32]; char model_id[64];
};

bool parseHeader(const uint8_t* bytes, size_t len, Header& h) {
    if (!bytes || len < kHeaderSize) return false;
    if (std::memcmp(bytes, "TDFMODEL", 8) != 0) return false;
    Reader r(bytes + 8, len - 8);
    h.format_version = r.u32(); h.header_size = r.u32(); h.n = r.u32(); h.e = r.u32();
    h.m_in = r.u32(); h.m_out = r.u32(); h.max_delay = r.u32(); h.flags = r.u32();
    h.payload_len = r.u64();
    const uint8_t* sha = r.take<uint8_t>(32); if (!sha) return false; std::memcpy(h.sha, sha, 32);
    const char* id = r.take<char>(64); if (!id) return false; std::memcpy(h.model_id, id, 64); h.model_id[63] = 0;
    if (h.format_version != kFormatVersion || h.header_size != kHeaderSize) return false;
    if (h.payload_len != len - kHeaderSize) return false;
    return true;
}

} // namespace

struct fc_handle {
    // topology (immutable after create)
    uint32_t n = 0, e = 0, m_in = 0, m_out = 0, max_delay = 1;
    fc_model_info info{};
    std::vector<float> v_rest, v_thresh, v_reset, tau_m, t_ref, tau_syn, bias;
    std::vector<uint32_t> row_ptr, post; std::vector<float> weight; std::vector<uint16_t> delay;
    std::vector<uint32_t> in_ptr, in_neuron; std::vector<float> in_rate, in_weight;
    std::vector<uint32_t> out_ptr, out_neuron; std::vector<float> out_gain; float out_tau[FC_MOTOR_CHANNELS]{}; float out_scale[FC_MOTOR_CHANNELS]{};
    std::vector<uint8_t> edge_mask; bool mask_active = false;
    // per-neuron precomputation
    std::vector<float> decay_m, decay_syn; std::vector<uint32_t> ref_steps;
    // dynamic state
    std::vector<float> v, g; std::vector<uint32_t> ref_left;
    std::vector<float> delay_buf;  // (max_delay) × n, ring
    uint32_t ring_head = 0; uint64_t t_ms = 0;
    float out_act[FC_MOTOR_CHANNELS]{}; std::vector<float> out_accum; // per channel, spikes accumulated in the ms
    std::vector<uint32_t> spike_counts; std::vector<uint32_t> spike_list; std::vector<uint8_t> spiked; // spikes of this ms
    std::vector<float> in_value; // 41 values of the current frame
    Rng rng;

    void resetState(uint64_t seed) {
        std::fill(v.begin(), v.end(), 0.f);
        for (uint32_t i = 0; i < n; ++i) v[i] = v_rest[i];
        std::fill(g.begin(), g.end(), 0.f);
        std::fill(ref_left.begin(), ref_left.end(), 0u);
        std::fill(delay_buf.begin(), delay_buf.end(), 0.f);
        ring_head = 0; t_ms = 0;
        for (auto& a : out_act) a = 0.f;
        std::fill(spike_counts.begin(), spike_counts.end(), 0u);
        rng.seed(seed);
    }

    // one 1 ms sub-step. Returns false on NaN/Inf.
    bool stepMs() {
        const float dt = 1.0f;
        // 1) Poisson inputs (Bernoulli per ms)
        for (uint32_t c = 0; c < kInputChannels; ++c) {
            const float x = in_value[c];
            if (x <= 0.f) continue;
            for (uint32_t k = in_ptr[c]; k < in_ptr[c + 1]; ++k) {
                float p = in_rate[k] * x; if (p > 1.f) p = 1.f;
                if (rng.uniform() < p) g[in_neuron[k]] += in_weight[k];
            }
        }
        // 2) deliver delayed events due now
        {
            float* slot = &delay_buf[(size_t)ring_head * n];
            for (uint32_t i = 0; i < n; ++i) { g[i] += slot[i]; slot[i] = 0.f; }
        }
        // 3) integrate membranes and detect spikes
        uint32_t nspk = 0;
        for (uint32_t i = 0; i < n; ++i) {
            float gi = g[i];
            if (ref_left[i] > 0) { --ref_left[i]; v[i] = v_reset[i]; }
            else {
                // exponential Euler on v with g treated as constant within the ms
                const float target = v_rest[i] + gi + bias[i];
                v[i] = target + (v[i] - target) * decay_m[i];
                if (v[i] >= v_thresh[i]) { v[i] = v_reset[i]; ref_left[i] = ref_steps[i]; spike_list[nspk++] = i; }
            }
            g[i] = gi * decay_syn[i];
        }
        // 4) propagate spikes into the ring buffer (with delay) and accumulate outputs
        for (uint32_t s = 0; s < nspk; ++s) {
            const uint32_t i = spike_list[s];
            ++spike_counts[i];
            const uint32_t a = row_ptr[i], b = row_ptr[i + 1];
            if (mask_active) {
                for (uint32_t k = a; k < b; ++k) {
                    if (edge_mask[k]) continue;
                    const uint32_t slot = (ring_head + delay[k]) % max_delay;
                    delay_buf[(size_t)slot * n + post[k]] += weight[k];
                }
            } else {
                for (uint32_t k = a; k < b; ++k) {
                    const uint32_t slot = (ring_head + delay[k]) % max_delay;
                    delay_buf[(size_t)slot * n + post[k]] += weight[k];
                }
            }
        }
        // outputs: accumulate spikes per channel using a per-neuron marker (cleared via spike_list)
        for (uint32_t s = 0; s < nspk; ++s) spiked[spike_list[s]] = 1;
        for (uint32_t c = 0; c < FC_MOTOR_CHANNELS; ++c) {
            float acc = 0.f;
            if (nspk) for (uint32_t k = out_ptr[c]; k < out_ptr[c + 1]; ++k) if (spiked[out_neuron[k]]) acc += out_gain[k];
            out_accum[c] = acc;
        }
        for (uint32_t s = 0; s < nspk; ++s) spiked[spike_list[s]] = 0;
        for (uint32_t c = 0; c < FC_MOTOR_CHANNELS; ++c) {
            const float tau = out_tau[c] > 0.f ? out_tau[c] : 1.f;
            out_act[c] += (out_accum[c] * out_scale[c] - out_act[c]) * (dt / tau);
            if (!std::isfinite(out_act[c])) return false;
        }
        ring_head = (ring_head + 1) % max_delay;
        ++t_ms;
        last_spikes = nspk;
        return true;
    }
    uint32_t last_spikes = 0;
};

namespace {

fc_status loadInto(fc_handle& H, const uint8_t* bytes, size_t len, const Header& h) {
    if (h.n == 0 || h.n > kMaxNeurons || h.e > kMaxEdges || h.max_delay == 0 || h.max_delay > kMaxDelay) return FC_E_LIMIT;
    if (h.m_in > kMaxInputEntries || h.m_out > kMaxOutputEntries) return FC_E_LIMIT;
    Reader r(bytes + kHeaderSize, len - kHeaderSize);
    const uint32_t n = h.n, e = h.e;
    // 1) parameters
    const float* prm = r.take<float>((size_t)n * 8); if (!prm) return FC_E_BAD_MODEL;
    // 2) edges
    const uint32_t* row_ptr = r.take<uint32_t>((size_t)n + 1); if (!row_ptr) return FC_E_BAD_MODEL;
    const uint32_t* post = r.take<uint32_t>(e); if (e && !post) return FC_E_BAD_MODEL;
    const float* weight = r.take<float>(e); if (e && !weight) return FC_E_BAD_MODEL;
    const uint16_t* delay = r.take<uint16_t>(e); if (e && !delay) return FC_E_BAD_MODEL;
    // 3) inputs
    const uint32_t* in_ptr = r.take<uint32_t>(kInputChannels + 1); if (!in_ptr) return FC_E_BAD_MODEL;
    const uint32_t* in_neuron = r.take<uint32_t>(h.m_in); if (h.m_in && !in_neuron) return FC_E_BAD_MODEL;
    const float* in_rate = r.take<float>(h.m_in); if (h.m_in && !in_rate) return FC_E_BAD_MODEL;
    const float* in_weight = r.take<float>(h.m_in); if (h.m_in && !in_weight) return FC_E_BAD_MODEL;
    // 4) outputs
    const uint32_t* out_ptr = r.take<uint32_t>(FC_MOTOR_CHANNELS + 1); if (!out_ptr) return FC_E_BAD_MODEL;
    const uint32_t* out_neuron = r.take<uint32_t>(h.m_out); if (h.m_out && !out_neuron) return FC_E_BAD_MODEL;
    const float* out_gain = r.take<float>(h.m_out); if (h.m_out && !out_gain) return FC_E_BAD_MODEL;
    const float* out_tau = r.take<float>(FC_MOTOR_CHANNELS); if (!out_tau) return FC_E_BAD_MODEL;
    const float* out_scale = r.take<float>(FC_MOTOR_CHANNELS); if (!out_scale) return FC_E_BAD_MODEL;
    // 5) provenance
    const uint32_t prov_len = r.u32(); if (!r.ok) return FC_E_BAD_MODEL;
    if (!r.take<uint8_t>(prov_len)) return FC_E_BAD_MODEL;
    if (r.off != r.len) return FC_E_BAD_MODEL; // extra bytes

    // validate indices and values BEFORE allocating
    if (Reader::readU32(&row_ptr[0]) != 0 || Reader::readU32(&row_ptr[n]) != e) return FC_E_BAD_MODEL;
    for (uint32_t i = 0; i < n; ++i) if (Reader::readU32(&row_ptr[i]) > Reader::readU32(&row_ptr[i + 1])) return FC_E_BAD_MODEL;
    for (uint32_t k = 0; k < e; ++k) {
        if (Reader::readU32(&post[k]) >= n) return FC_E_BAD_MODEL;
        uint16_t d; std::memcpy(&d, &delay[k], 2); if (d == 0 || d > h.max_delay) return FC_E_BAD_MODEL;
        float w; std::memcpy(&w, &weight[k], 4); if (!std::isfinite(w)) return FC_E_BAD_MODEL;
    }
    if (Reader::readU32(&in_ptr[0]) != 0 || Reader::readU32(&in_ptr[kInputChannels]) != h.m_in) return FC_E_BAD_MODEL;
    for (uint32_t c = 0; c < kInputChannels; ++c) if (Reader::readU32(&in_ptr[c]) > Reader::readU32(&in_ptr[c + 1])) return FC_E_BAD_MODEL;
    for (uint32_t k = 0; k < h.m_in; ++k) { if (Reader::readU32(&in_neuron[k]) >= n) return FC_E_BAD_MODEL; float a, b; std::memcpy(&a, &in_rate[k], 4); std::memcpy(&b, &in_weight[k], 4); if (!std::isfinite(a) || !std::isfinite(b) || a < 0.f) return FC_E_BAD_MODEL; }
    if (Reader::readU32(&out_ptr[0]) != 0 || Reader::readU32(&out_ptr[FC_MOTOR_CHANNELS]) != h.m_out) return FC_E_BAD_MODEL;
    for (uint32_t c = 0; c < FC_MOTOR_CHANNELS; ++c) if (Reader::readU32(&out_ptr[c]) > Reader::readU32(&out_ptr[c + 1])) return FC_E_BAD_MODEL;
    for (uint32_t k = 0; k < h.m_out; ++k) { if (Reader::readU32(&out_neuron[k]) >= n) return FC_E_BAD_MODEL; float a; std::memcpy(&a, &out_gain[k], 4); if (!std::isfinite(a)) return FC_E_BAD_MODEL; }
    for (uint32_t i = 0; i < n; ++i) for (uint32_t j = 0; j < 8; ++j) { float x; std::memcpy(&x, &prm[(size_t)i * 8 + j], 4); if (!std::isfinite(x)) return FC_E_BAD_MODEL; }
    for (uint32_t i = 0; i < n; ++i) { float tm, tr, ts; std::memcpy(&tm, &prm[(size_t)i*8+3], 4); std::memcpy(&tr, &prm[(size_t)i*8+4], 4); std::memcpy(&ts, &prm[(size_t)i*8+5], 4); if (tm <= 0.f || ts <= 0.f || tr < 0.f) return FC_E_BAD_MODEL; }

    // allocate and copy (the only allocation phase). The compiled limits (kMax*) bound the memory to < 64 MB;
    // with exceptions disabled (Android/Web) an allocation failure aborts, which is acceptable within those limits.
#if defined(__cpp_exceptions) || defined(_CPPUNWIND)
    try {
#endif
    {
        H.n = n; H.e = e; H.m_in = h.m_in; H.m_out = h.m_out; H.max_delay = h.max_delay;
        H.v_rest.resize(n); H.v_thresh.resize(n); H.v_reset.resize(n); H.tau_m.resize(n); H.t_ref.resize(n); H.tau_syn.resize(n); H.bias.resize(n);
        for (uint32_t i = 0; i < n; ++i) {
            const float* q = prm + (size_t)i * 8;
            std::memcpy(&H.v_rest[i], q + 0, 4); std::memcpy(&H.v_thresh[i], q + 1, 4); std::memcpy(&H.v_reset[i], q + 2, 4);
            std::memcpy(&H.tau_m[i], q + 3, 4); std::memcpy(&H.t_ref[i], q + 4, 4); std::memcpy(&H.tau_syn[i], q + 5, 4); std::memcpy(&H.bias[i], q + 6, 4);
        }
        H.row_ptr.resize((size_t)n + 1); std::memcpy(H.row_ptr.data(), row_ptr, ((size_t)n + 1) * 4);
        H.post.resize(e); H.weight.resize(e); H.delay.resize(e);
        if (e) { std::memcpy(H.post.data(), post, (size_t)e * 4); std::memcpy(H.weight.data(), weight, (size_t)e * 4); std::memcpy(H.delay.data(), delay, (size_t)e * 2); }
        H.in_ptr.resize(kInputChannels + 1); std::memcpy(H.in_ptr.data(), in_ptr, (kInputChannels + 1) * 4);
        H.in_neuron.resize(h.m_in); H.in_rate.resize(h.m_in); H.in_weight.resize(h.m_in);
        if (h.m_in) { std::memcpy(H.in_neuron.data(), in_neuron, (size_t)h.m_in * 4); std::memcpy(H.in_rate.data(), in_rate, (size_t)h.m_in * 4); std::memcpy(H.in_weight.data(), in_weight, (size_t)h.m_in * 4); }
        H.out_ptr.resize(FC_MOTOR_CHANNELS + 1); std::memcpy(H.out_ptr.data(), out_ptr, (FC_MOTOR_CHANNELS + 1) * 4);
        H.out_neuron.resize(h.m_out); H.out_gain.resize(h.m_out);
        if (h.m_out) { std::memcpy(H.out_neuron.data(), out_neuron, (size_t)h.m_out * 4); std::memcpy(H.out_gain.data(), out_gain, (size_t)h.m_out * 4); }
        std::memcpy(H.out_tau, out_tau, FC_MOTOR_CHANNELS * 4); std::memcpy(H.out_scale, out_scale, FC_MOTOR_CHANNELS * 4);
        H.edge_mask.assign(e, 0);
        H.decay_m.resize(n); H.decay_syn.resize(n); H.ref_steps.resize(n);
        for (uint32_t i = 0; i < n; ++i) { H.decay_m[i] = std::exp(-1.0f / H.tau_m[i]); H.decay_syn[i] = std::exp(-1.0f / H.tau_syn[i]); H.ref_steps[i] = (uint32_t)std::lround(H.t_ref[i]); }
        H.v.resize(n); H.g.resize(n); H.ref_left.resize(n);
        H.delay_buf.assign((size_t)H.max_delay * n, 0.f);
        H.out_accum.assign(FC_MOTOR_CHANNELS, 0.f);
        H.spike_counts.assign(n, 0u); H.spike_list.assign(n, 0u); H.spiked.assign(n, 0);
        H.in_value.assign(kInputChannels, 0.f);
    }
#if defined(__cpp_exceptions) || defined(_CPPUNWIND)
    } catch (const std::bad_alloc&) { return FC_E_OOM; }
#endif
    H.info.struct_size = sizeof(fc_model_info); H.info.format_version = h.format_version; H.info.neuron_count = n; H.info.edge_count = e;
    H.info.input_channel_count = kInputChannels; H.info.output_channel_count = FC_MOTOR_CHANNELS; H.info.max_delay_steps = h.max_delay;
    std::memcpy(H.info.payload_sha256, h.sha, 32); std::memcpy(H.info.model_id, h.model_id, 64);
    return FC_OK;
}

bool verifyHash(const uint8_t* bytes, size_t len, const Header& h) {
    uint8_t digest[32]; fc_sha256(bytes + kHeaderSize, len - kHeaderSize, digest);
    return std::memcmp(digest, h.sha, 32) == 0;
}

} // namespace

extern "C" {

FC_API uint32_t fc_abi_version(void) { return FC_ABI_VERSION; }

FC_API fc_status fc_inspect(const uint8_t* model_bytes, size_t model_len, fc_model_info* out_info) {
    if (!out_info || out_info->struct_size != sizeof(fc_model_info)) return FC_E_ABI;
    Header h{}; if (!parseHeader(model_bytes, model_len, h)) return FC_E_BAD_MODEL;
    if (!verifyHash(model_bytes, model_len, h)) return FC_E_HASH_MISMATCH;
    fc_handle tmp; fc_status st = loadInto(tmp, model_bytes, model_len, h); if (st != FC_OK) return st;
    *out_info = tmp.info; return FC_OK;
}

FC_API fc_status fc_create(const uint8_t* model_bytes, size_t model_len, uint64_t seed, fc_handle** out_handle) {
    if (!out_handle) return FC_E_INVALID_ARG; *out_handle = nullptr;
    Header h{}; if (!parseHeader(model_bytes, model_len, h)) return FC_E_BAD_MODEL;
    if (!verifyHash(model_bytes, model_len, h)) return FC_E_HASH_MISMATCH;
    fc_handle* H = new (std::nothrow) fc_handle(); if (!H) return FC_E_OOM;
    fc_status st = loadInto(*H, model_bytes, model_len, h); if (st != FC_OK) { delete H; return st; }
    H->resetState(seed); *out_handle = H; return FC_OK;
}

FC_API fc_status fc_step(fc_handle* h, const fc_sensory_frame* in, uint32_t delta_ms, fc_motor_frame* out) {
    if (!h || !in || !out) return FC_E_INVALID_ARG;
    if (in->struct_size != sizeof(fc_sensory_frame) || in->version != FC_ABI_VERSION) return FC_E_ABI;
    if (out->struct_size != sizeof(fc_motor_frame) || out->version != FC_ABI_VERSION) return FC_E_ABI;
    if (delta_ms == 0 || delta_ms > 1000) return FC_E_INVALID_ARG;
    // copy the frame into the channel vector (kind×8+sector; background at 40)
    for (uint32_t s = 0; s < FC_SECTORS; ++s) {
        h->in_value[0 * FC_SECTORS + s] = in->expansion[s];
        h->in_value[1 * FC_SECTORS + s] = in->angular_size[s];
        h->in_value[2 * FC_SECTORS + s] = in->motion_h[s];
        h->in_value[3 * FC_SECTORS + s] = in->motion_v[s];
        h->in_value[4 * FC_SECTORS + s] = in->surface_proximity[s];
    }
    h->in_value[5 * FC_SECTORS] = in->background_drive;
    for (uint32_t c = 0; c < kInputChannels; ++c) if (!std::isfinite(h->in_value[c])) return FC_E_INVALID_ARG;
    uint32_t spikes = 0;
    for (uint32_t k = 0; k < delta_ms; ++k) { if (!h->stepMs()) return FC_E_NUMERIC; spikes += h->last_spikes; }
    for (uint32_t c = 0; c < FC_MOTOR_CHANNELS; ++c) out->channel[c] = h->out_act[c];
    out->spikes_this_step = spikes;
    out->flags = (h->out_act[0] + h->out_act[1] > 0.5f) ? 1u : 0u;
    return FC_OK;
}

FC_API fc_status fc_reset(fc_handle* h, uint64_t seed) { if (!h) return FC_E_INVALID_ARG; h->resetState(seed); return FC_OK; }

FC_API fc_status fc_snapshot(const fc_handle* h, uint8_t* buf, size_t* inout_len) {
    if (!h || !inout_len) return FC_E_INVALID_ARG;
    const size_t need = 8 + 4 + sizeof(h->rng.s) + (size_t)h->n * (4 + 4 + 4) + (size_t)h->max_delay * h->n * 4 + FC_MOTOR_CHANNELS * 4;
    if (!buf) { *inout_len = need; return FC_OK; }
    if (*inout_len < need) { *inout_len = need; return FC_E_INVALID_ARG; }
    uint8_t* p = buf;
    std::memcpy(p, &h->t_ms, 8); p += 8; std::memcpy(p, &h->ring_head, 4); p += 4; std::memcpy(p, h->rng.s, sizeof(h->rng.s)); p += sizeof(h->rng.s);
    std::memcpy(p, h->v.data(), (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(p, h->g.data(), (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(p, h->ref_left.data(), (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(p, h->delay_buf.data(), (size_t)h->max_delay * h->n * 4); p += (size_t)h->max_delay * h->n * 4;
    std::memcpy(p, h->out_act, FC_MOTOR_CHANNELS * 4);
    *inout_len = need; return FC_OK;
}

FC_API fc_status fc_restore(fc_handle* h, const uint8_t* buf, size_t len) {
    if (!h || !buf) return FC_E_INVALID_ARG;
    size_t need = 0; fc_snapshot(h, nullptr, &need); if (len != need) return FC_E_INVALID_ARG;
    const uint8_t* p = buf;
    std::memcpy(&h->t_ms, p, 8); p += 8; std::memcpy(&h->ring_head, p, 4); p += 4; std::memcpy(h->rng.s, p, sizeof(h->rng.s)); p += sizeof(h->rng.s);
    if (h->ring_head >= h->max_delay) return FC_E_INVALID_ARG;
    std::memcpy(h->v.data(), p, (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(h->g.data(), p, (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(h->ref_left.data(), p, (size_t)h->n * 4); p += (size_t)h->n * 4;
    std::memcpy(h->delay_buf.data(), p, (size_t)h->max_delay * h->n * 4); p += (size_t)h->max_delay * h->n * 4;
    std::memcpy(h->out_act, p, FC_MOTOR_CHANNELS * 4);
    return FC_OK;
}

FC_API fc_status fc_set_edge_mask(fc_handle* h, const uint8_t* edge_mask, size_t edge_mask_len) {
    if (!h || !edge_mask || edge_mask_len != h->e) return FC_E_INVALID_ARG;
    std::memcpy(h->edge_mask.data(), edge_mask, edge_mask_len); h->mask_active = true; return FC_OK;
}
FC_API fc_status fc_clear_edge_mask(fc_handle* h) { if (!h) return FC_E_INVALID_ARG; std::fill(h->edge_mask.begin(), h->edge_mask.end(), 0); h->mask_active = false; return FC_OK; }

FC_API fc_status fc_read_spike_counts(const fc_handle* h, uint32_t* out_counts, size_t out_len) {
    if (!h || !out_counts || out_len != h->n) return FC_E_INVALID_ARG;
    std::memcpy(out_counts, h->spike_counts.data(), (size_t)h->n * 4); return FC_OK;
}
FC_API fc_status fc_clear_spike_counts(fc_handle* h) { if (!h) return FC_E_INVALID_ARG; std::fill(h->spike_counts.begin(), h->spike_counts.end(), 0u); return FC_OK; }

FC_API fc_status fc_get_info(const fc_handle* h, fc_model_info* out_info) {
    if (!h || !out_info || out_info->struct_size != sizeof(fc_model_info)) return FC_E_ABI;
    *out_info = h->info; return FC_OK;
}

FC_API void fc_destroy(fc_handle* h) { delete h; }

} // extern "C"
