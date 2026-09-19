// In-memory .tdfm package builder (tests, bench and parity). Not part of the game runtime.
#pragma once
#include <vector>
#include <string>
#include <cstdint>

struct NeuronParams { float v_rest = -52.f, v_thresh = -45.f, v_reset = -52.f, tau_m = 20.f, t_ref = 2.f, tau_syn = 5.f, bias = 0.f, reserved = 0.f; };
struct Edge { uint32_t pre, post; float weight_mv; uint16_t delay; };
struct InputEntry { uint32_t channel, neuron; float rate_per_ms, weight_mv; };
struct OutputEntry { uint32_t channel, neuron; float gain; };

struct ModelSpec {
    std::string model_id = "test";
    std::vector<NeuronParams> neurons;
    std::vector<Edge> edges;
    std::vector<InputEntry> inputs;
    std::vector<OutputEntry> outputs;
    float out_tau[8] = {20,20,20,20,20,20,20,20};
    float out_scale[8] = {1,1,1,1,1,1,1,1};
    uint32_t max_delay = 8;
    std::string provenance_json = "{}";
};

std::vector<uint8_t> buildModel(const ModelSpec& spec);
