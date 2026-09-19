// flycore_cli: inspects a .tdfm or runs a deterministic trial from a stimuli file.
// Used by research for Python <-> C++ parity and for the causal trials with the real runtime.
//   flycore_cli inspect model.tdfm
//   flycore_cli run model.tdfm seed stimuli.bin out.bin [mask.bin]
//   stimuli.bin: T frames x 41 float32 (channels in kind*8+sector order; 40 = background)
//   out.bin:     T x (8 float32 channels + u32 spikes) followed by neuron_count x u32 (spike counts)
#include "flycore.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>

static std::vector<uint8_t> readAll(const char* path) {
    std::vector<uint8_t> b; FILE* f = std::fopen(path, "rb"); if (!f) return b;
    std::fseek(f, 0, SEEK_END); long n = std::ftell(f); std::fseek(f, 0, SEEK_SET);
    b.resize((size_t)n); if (n > 0 && std::fread(b.data(), 1, (size_t)n, f) != (size_t)n) b.clear();
    std::fclose(f); return b;
}

int main(int argc, char** argv) {
    if (argc < 3) { std::puts("usage: flycore_cli inspect model.tdfm | run model.tdfm seed stimuli.bin out.bin [mask.bin]"); return 2; }
    auto model = readAll(argv[2]); if (model.empty()) { std::puts("cannot read model"); return 1; }
    if (std::strcmp(argv[1], "inspect") == 0) {
        fc_model_info info{}; info.struct_size = sizeof(info); fc_status st = fc_inspect(model.data(), model.size(), &info);
        std::printf("status=%d id=%s neurons=%u edges=%u max_delay=%u sha256=", (int)st, info.model_id, info.neuron_count, info.edge_count, info.max_delay_steps);
        for (int i = 0; i < 32; ++i) std::printf("%02x", info.payload_sha256[i]);
        std::puts(""); return st == FC_OK ? 0 : 1;
    }
    if (std::strcmp(argv[1], "run") == 0 && argc >= 6) {
        uint64_t seed = std::strtoull(argv[3], nullptr, 10); auto stim = readAll(argv[4]);
        const size_t frameBytes = 41 * sizeof(float);
        if (stim.empty() || stim.size() % frameBytes != 0) { std::puts("bad stimuli size"); return 1; }
        size_t T = stim.size() / frameBytes;
        fc_handle* h; fc_status st = fc_create(model.data(), model.size(), seed, &h);
        if (st != FC_OK) { std::printf("create failed %d\n", (int)st); return 1; }
        if (argc >= 7) { auto mask = readAll(argv[6]); if (fc_set_edge_mask(h, mask.data(), mask.size()) != FC_OK) { std::puts("bad mask"); return 1; } }
        fc_model_info info{}; info.struct_size = sizeof(info); fc_get_info(h, &info);
        std::vector<uint8_t> out; out.reserve(T * (8 * 4 + 4));
        fc_sensory_frame in{}; in.struct_size = sizeof(in); in.version = FC_ABI_VERSION;
        fc_motor_frame mo{}; mo.struct_size = sizeof(mo); mo.version = FC_ABI_VERSION;
        for (size_t t = 0; t < T; ++t) {
            const float* f = reinterpret_cast<const float*>(stim.data() + t * frameBytes);
            for (int s = 0; s < 8; ++s) { in.expansion[s] = f[s]; in.angular_size[s] = f[8 + s]; in.motion_h[s] = f[16 + s]; in.motion_v[s] = f[24 + s]; in.surface_proximity[s] = f[32 + s]; }
            in.background_drive = f[40];
            st = fc_step(h, &in, 10, &mo); if (st != FC_OK) { std::printf("step failed %d at t=%zu\n", (int)st, t); return 1; }
            const uint8_t* p = reinterpret_cast<const uint8_t*>(mo.channel); out.insert(out.end(), p, p + 32);
            p = reinterpret_cast<const uint8_t*>(&mo.spikes_this_step); out.insert(out.end(), p, p + 4);
        }
        std::vector<uint32_t> counts(info.neuron_count); fc_read_spike_counts(h, counts.data(), counts.size());
        FILE* fo = std::fopen(argv[5], "wb"); if (!fo) return 1;
        std::fwrite(out.data(), 1, out.size(), fo); std::fwrite(counts.data(), 4, counts.size(), fo); std::fclose(fo);
        fc_destroy(h); return 0;
    }
    std::puts("bad args"); return 2;
}
