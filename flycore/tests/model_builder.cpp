#include "model_builder.h"
#include "sha256.h"
#include <algorithm>
#include <cstring>

namespace {
template <class T> void put(std::vector<uint8_t>& b, const T& v) { const uint8_t* p = reinterpret_cast<const uint8_t*>(&v); b.insert(b.end(), p, p + sizeof(T)); }
}

std::vector<uint8_t> buildModel(const ModelSpec& s) {
    const uint32_t n = (uint32_t)s.neurons.size();
    std::vector<uint8_t> payload;
    for (const auto& p : s.neurons) { put(payload, p.v_rest); put(payload, p.v_thresh); put(payload, p.v_reset); put(payload, p.tau_m); put(payload, p.t_ref); put(payload, p.tau_syn); put(payload, p.bias); put(payload, p.reserved); }
    // CSR by pre
    std::vector<Edge> edges = s.edges; std::stable_sort(edges.begin(), edges.end(), [](const Edge& a, const Edge& b){ return a.pre < b.pre; });
    std::vector<uint32_t> row_ptr(n + 1, 0); for (const auto& e : edges) ++row_ptr[e.pre + 1]; for (uint32_t i = 0; i < n; ++i) row_ptr[i + 1] += row_ptr[i];
    for (auto v : row_ptr) put(payload, v);
    for (const auto& e : edges) put(payload, e.post);
    for (const auto& e : edges) put(payload, e.weight_mv);
    for (const auto& e : edges) put(payload, e.delay);
    // inputs (41 channels)
    std::vector<InputEntry> ins = s.inputs; std::stable_sort(ins.begin(), ins.end(), [](const InputEntry& a, const InputEntry& b){ return a.channel < b.channel; });
    std::vector<uint32_t> in_ptr(42, 0); for (const auto& e : ins) ++in_ptr[e.channel + 1]; for (uint32_t i = 0; i < 41; ++i) in_ptr[i + 1] += in_ptr[i];
    for (auto v : in_ptr) put(payload, v);
    for (const auto& e : ins) put(payload, e.neuron);
    for (const auto& e : ins) put(payload, e.rate_per_ms);
    for (const auto& e : ins) put(payload, e.weight_mv);
    // outputs (8 channels)
    std::vector<OutputEntry> outs = s.outputs; std::stable_sort(outs.begin(), outs.end(), [](const OutputEntry& a, const OutputEntry& b){ return a.channel < b.channel; });
    std::vector<uint32_t> out_ptr(9, 0); for (const auto& e : outs) ++out_ptr[e.channel + 1]; for (uint32_t i = 0; i < 8; ++i) out_ptr[i + 1] += out_ptr[i];
    for (auto v : out_ptr) put(payload, v);
    for (const auto& e : outs) put(payload, e.neuron);
    for (const auto& e : outs) put(payload, e.gain);
    for (int c = 0; c < 8; ++c) put(payload, s.out_tau[c]);
    for (int c = 0; c < 8; ++c) put(payload, s.out_scale[c]);
    // provenance
    put(payload, (uint32_t)s.provenance_json.size()); payload.insert(payload.end(), s.provenance_json.begin(), s.provenance_json.end());

    std::vector<uint8_t> out; out.reserve(144 + payload.size());
    const char magic[8] = {'T','D','F','M','O','D','E','L'}; out.insert(out.end(), magic, magic + 8);
    put(out, (uint32_t)1); put(out, (uint32_t)144); put(out, n); put(out, (uint32_t)edges.size()); put(out, (uint32_t)ins.size()); put(out, (uint32_t)outs.size()); put(out, s.max_delay); put(out, (uint32_t)0);
    put(out, (uint64_t)payload.size());
    uint8_t sha[32]; fc_sha256(payload.data(), payload.size(), sha); out.insert(out.end(), sha, sha + 32);
    char id[64] = {0}; std::strncpy(id, s.model_id.c_str(), 63); out.insert(out.end(), id, id + 64);
    out.insert(out.end(), payload.begin(), payload.end());
    return out;
}
