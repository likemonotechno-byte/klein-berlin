#ifndef RIMCONTROLLER_H
#define RIMCONTROLLER_H
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
#define RC_ABI_VERSION 1
#pragma pack(push, 4)
typedef struct {
    uint32_t size;
    uint32_t abi;
    uint64_t device_id;
    uint64_t buttons;
    uint32_t capabilities;
    uint32_t model;
    uint32_t connected;
    float left_x, left_y, right_x, right_y, left_trigger, right_trigger;
    char name[128];
} rc_snapshot;
#pragma pack(pop)
// Caller owns storage. Call only on the application's main thread.
// 0 success (including no device), -1 size mismatch, -2 ABI mismatch, -3 wrong thread.
int32_t rc_poll(rc_snapshot *snapshot, uint32_t size, uint32_t abi);
#ifdef __cplusplus
}
#endif
#endif
