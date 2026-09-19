/*
 * FlyCore - local neural simulator of That Damn Fly.
 * Stable C ABI, C++17 inside. Same implementation on Windows (editor), Web (Emscripten via Unity),
 * Android (ARM64 .so) and iOS (source compiled by Xcode).
 *
 * Boundary rules (M0 contract):
 *  - fc_sensory_frame only carries stimuli materialized in the world (visual expansion, relative
 *    motion, proximity of surfaces). Never the finger/aim position, tool, fury or history.
 *  - fc_motor_frame returns filtered population activity; the body converts it into velocity/rotation.
 *  - No allocations per step, no callbacks, no I/O.
 */
#ifndef FLYCORE_H
#define FLYCORE_H

#include <stdint.h>
#include <stddef.h>

#if defined(_WIN32) && !defined(FC_STATIC)
#  ifdef FC_BUILD
#    define FC_API __declspec(dllexport)
#  else
#    define FC_API __declspec(dllimport)
#  endif
#elif defined(__GNUC__) || defined(__clang__)
#  define FC_API __attribute__((visibility("default")))
#else
#  define FC_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define FC_ABI_VERSION 1u
#define FC_SECTORS 8u          /* visual sectors around the fly (azimuth, body frame) */
#define FC_MOTOR_CHANNELS 8u   /* output channels (see fc_motor_frame) */

typedef struct fc_handle fc_handle;

typedef enum fc_status {
    FC_OK = 0,
    FC_E_INVALID_ARG = 1,
    FC_E_BAD_MODEL = 2,       /* invalid format, magic, version, counts or indices */
    FC_E_HASH_MISMATCH = 3,   /* payload hash does not match the header */
    FC_E_LIMIT = 4,           /* exceeds compiled limits (neurons/edges/delays) */
    FC_E_NUMERIC = 5,         /* NaN/Inf detected: stop the round */
    FC_E_OOM = 6,
    FC_E_ABI = 7              /* unknown struct_size/version */
} fc_status;

/* Stimuli per world tick (10 ms). All values are dimensionless and already normalized by the
 * game's SensoryEncoder according to the documented model (docs/SENSORY_MODEL.md). */
typedef struct fc_sensory_frame {
    uint32_t struct_size;               /* = sizeof(fc_sensory_frame) */
    uint32_t version;                   /* = FC_ABI_VERSION */
    float expansion[FC_SECTORS];        /* angular expansion rate (looming) per sector, >= 0 */
    float angular_size[FC_SECTORS];     /* current angular size of the most threatening object per sector, [0,1] */
    float motion_h[FC_SECTORS];         /* horizontal relative motion per sector, [-1,1] */
    float motion_v[FC_SECTORS];         /* vertical relative motion per sector, [-1,1] */
    float surface_proximity[FC_SECTORS];/* edge/surface proximity per sector, [0,1] */
    float background_drive;             /* background excitation (model parameter, normally 1.0) */
} fc_sensory_frame;

/* Output per tick: filtered activity (0..~1) of readout populations. The motor meaning of each
 * channel is a simulator HYPOTHESIS documented in the model manifest. */
typedef struct fc_motor_frame {
    uint32_t struct_size;
    uint32_t version;
    float channel[FC_MOTOR_CHANNELS];   /* 0: escape_L 1: escape_R 2: turn_L 3: turn_R 4: fwd_L 5: fwd_R 6,7: reserved */
    uint32_t spikes_this_step;          /* diagnostics: total spikes in the tick */
    uint32_t flags;                     /* bit0: escape response active (internal diagnostic threshold) */
} fc_motor_frame;

typedef struct fc_model_info {
    uint32_t struct_size;
    uint32_t format_version;
    uint32_t neuron_count;
    uint32_t edge_count;
    uint32_t input_channel_count;
    uint32_t output_channel_count;
    uint32_t max_delay_steps;
    uint32_t reserved;
    uint8_t  payload_sha256[32];
    char     model_id[64];              /* e.g. "tdf-escape-v0.1" (UTF-8, NUL-terminated) */
} fc_model_info;

FC_API uint32_t  fc_abi_version(void);

/* Inspects a package without loading it (validates header, counts, indices and hash). */
FC_API fc_status fc_inspect(const uint8_t* model_bytes, size_t model_len, fc_model_info* out_info);

/* Creates an instance from the package bytes. Copies what it needs; the caller may free model_bytes. */
FC_API fc_status fc_create(const uint8_t* model_bytes, size_t model_len, uint64_t seed, fc_handle** out_handle);

/* Advances delta_ms milliseconds (1 neural sub-step per ms). In M0 delta_ms = 10. */
FC_API fc_status fc_step(fc_handle* h, const fc_sensory_frame* in, uint32_t delta_ms, fc_motor_frame* out);

/* Resets states (membranes, queues, filters, RNG) with a new seed. Topology and weights untouched. */
FC_API fc_status fc_reset(fc_handle* h, uint64_t seed);

/* Test/pause tools: snapshot of the dynamic state. Call with buf=NULL to get the size. */
FC_API fc_status fc_snapshot(const fc_handle* h, uint8_t* buf, size_t* inout_len);
FC_API fc_status fc_restore(fc_handle* h, const uint8_t* buf, size_t len);

/* Controlled perturbation for causal trials (NOT used by the game). mask: 0 = edge active, 1 = silenced.
 * edge_mask_len must equal edge_count. */
FC_API fc_status fc_set_edge_mask(fc_handle* h, const uint8_t* edge_mask, size_t edge_mask_len);
FC_API fc_status fc_clear_edge_mask(fc_handle* h);

/* Diagnostics: spike count per neuron since the last reset/clear. */
FC_API fc_status fc_read_spike_counts(const fc_handle* h, uint32_t* out_counts, size_t out_len);
FC_API fc_status fc_clear_spike_counts(fc_handle* h);

FC_API fc_status fc_get_info(const fc_handle* h, fc_model_info* out_info);
FC_API void      fc_destroy(fc_handle* h);

#ifdef __cplusplus
}
#endif
#endif /* FLYCORE_H */
