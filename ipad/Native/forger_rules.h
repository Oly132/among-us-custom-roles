#pragma once
#include <stdint.h>

/* Rules-library ABI only. This header is not an Among Us loader or hook adapter. */
#ifdef __cplusplus
extern "C" {
#endif
typedef struct {
    uint32_t size, abi;
    uint8_t forger, silencer, thief, fat_guy, lockpicker, door_cooldown_enabled;
    uint16_t reserved;
    int32_t forge_uses;
    float forge_cooldown, forge_window;
    int32_t doors_per_use;
    float door_cooldown;
} forger_rules_config;
typedef struct {
    uint32_t size, epoch;
    uint8_t pending_victim, fat_guy_protection_spent, thief_used, silenced;
    int32_t forge_uses_remaining, silence_seconds_remaining, door_uses_remaining;
    double evidence_deadline, forge_ready_at, door_ready_at;
} forger_rules_snapshot_data;
enum forger_rules_event_kind {
    FORGER_KILLED=1, FORGE=2, APPLY_SILENCE=3, MEETING_STARTED=4,
    MEETING_ENDED=5, RULES_TICK=6, ORDINARY_STAB=7,
    OPEN_SABOTAGED_DOOR=8, TRY_STEAL=9
};
uint32_t forger_rules_abi(void);
uint32_t forger_rules_create(const forger_rules_config*, uint32_t epoch);
int32_t forger_rules_destroy(uint32_t handle);
int32_t forger_rules_event(uint32_t handle,int32_t kind,uint8_t actor,uint8_t target,uint8_t value,double now);
int32_t forger_rules_snapshot(uint32_t handle,uint8_t player,double now,forger_rules_snapshot_data* output);
#ifdef __cplusplus
}
static_assert(sizeof(forger_rules_config)==36,"Rules config ABI mismatch");
static_assert(sizeof(forger_rules_snapshot_data)==48,"Rules snapshot ABI mismatch");
#endif
