// Minimal SHA-256 (own implementation, FIPS 180-4). Used only to validate the package on load.
#ifndef FC_SHA256_H
#define FC_SHA256_H
#include <stdint.h>
#include <stddef.h>
void fc_sha256(const uint8_t* data, size_t len, uint8_t out[32]);
#endif
