namespace HM.Events
{
    /// <summary>Opcode names and confidence, generated from tools/hmconv/script.py OPS (H high, M medium, L low, U unknown).</summary>
    public static class OpInfo
    {
        public const int Count = 113;
        const string Confidence = "HHHMHHHMMMMMMHHHHHUHHHHMMMMMMMMMMLHMHMMUMLMMLLMMLLMMMMMMHMMMHHLLMLHLLHMMLHHLHLMHHMMLHLLHMULMLLHULMLMHLLMHMULLLLLL";
        static readonly string[] Names =
        {
            "move_axis", "move_anim", "dialog", "frame_timer", "set_pos", "anim",
            "set_hidden", "actor_map", "set_next_location", "camera_move", "camera_release", "sleep_trigger",
            "face_player", "cond_field", "cond_field_var", "cond_var", "cond_var_var", "cond_range",
            "op18", "set_field", "set_field_var", "var_set", "var_from_field", "wait_transition",
            "bgm_play", "bgm_default", "bgm_fade", "screen_fade", "input_lock", "item_count",
            "item_count2", "item_give", "item_give_notify", "name_input_wait", "near_player", "actor_reset",
            "time_passed", "script_end", "random", "op39", "animal_state", "animal_state4",
            "screen_tint", "actor_effect", "lookup_store", "large_routine", "inventory_stash", "print_number",
            "spawn_set", "spawn_one", "queue_event", "camera_follow", "slot_set", "slot_flag",
            "slot_offset", "slot_to_var", "check_mode3", "npc_pref_var", "item_pair_to_var", "check_state",
            "set_pos_vars", "set_player_pos", "camera_scroll", "emote", "set_face", "gift_event",
            "event_flag", "npc_unlocked", "call_vars", "save_bits", "sfx", "actor_clear_flags",
            "camera_scroll_vars", "actor_pos_to_vars", "event_pos_to_vars", "pending_check", "wait_until_time", "fade_wait",
            "actor_init", "return_addr_to_var", "return_jump", "big_switch", "move_run", "actor_stop_face",
            "flag_triplet", "table_lookup", "scatter", "cond_field_by_var", "set_field_by_var", "op89",
            "party_choreo", "set_field_all", "lookup_a", "call3", "actor_cell", "op95",
            "big_switch2", "set_field_visible", "menu_result", "mode_wait", "register_pair", "pick_candidates",
            "npc_interact", "face_actor", "actor_move_rel", "cond_flag_gate", "op106", "minigame_step",
            "show_face", "projectile", "rank_query", "rank_message", "coord_table"
        };

        public static string Name(int op) => op >= 0 && op < Count ? Names[op] : "op" + op;
        public static char Level(int op) => op >= 0 && op < Count ? Confidence[op] : 'U';
        public static string LevelName(int op)
        {
            switch (Level(op)) { case 'H': return "high"; case 'M': return "med"; case 'L': return "low"; default: return "unknown"; }
        }
    }
}
