using System.Collections.Generic;

/// <summary>
/// Menu text in every language: key → string. Loc.T("play") gives the current language (GameSettings.Language),
/// falling back to English, then to the key itself. To add a language: add a name to Languages and a table to Tables.
/// The in-race HUD is still English only.
/// </summary>
public static class Loc
{
    public static readonly string[] Languages = { "English", "Thai" }; // Latin names: always drawable, whatever font is loaded

    static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        // main page
        { "tagline", "ONE SCREEN, TWO DESTINIES" },
        { "play", "PLAY" }, { "howto", "HOW TO PLAY" }, { "settings", "SETTINGS" }, { "quit", "QUIT" }, { "back", "BACK" },
        // key prompts along the bottom
        { "hint_move", "Navigate" }, { "hint_select", "Select" }, { "hint_back", "Back" }, { "hint_adjust", "Adjust" },
        { "hint_choose", "Colour" }, { "hint_car", "Car" }, { "hint_ready", "Ready" },
        // play flow
        { "step_mode", "MODE" }, { "step_racers", "RACERS" }, { "step_color", "CAR" }, { "step_course", "COURSE" },
        { "mode_title", "SELECT MODE" }, { "mode_single", "SINGLE PLAYER" }, { "mode_two", "2 PLAYERS" },
        { "mode_single_sub", "Race against AI trucks" }, { "mode_two_sub", "Split screen, one keyboard" },
        { "racers_title", "RACERS IN THE MATCH" }, { "racers_label", "TRUCKS ON THE START GRID" },
        { "racers_note", "Players plus AI trucks, 2 to 6. The AI fills the empty places." },
        { "players_chip", "PLAYERS {0}" }, { "ai_chip", "AI TRUCKS {0}" }, { "continue", "CONTINUE" },
        // colour select
        { "color_title", "CHOOSE YOUR CAR" },
        { "stat_speed", "SPEED" }, { "stat_accel", "ACCEL" }, { "stat_handling", "HANDLING" }, { "stat_gems", "GEM STORAGE" },
        { "car_class_super", "Supercar" }, { "car_class_gt", "Sports GT" }, { "car_class_formula", "Formula racer" },
        { "car_class_hatch", "Hot hatch" }, { "car_class_truck", "Gem truck" }, { "player_n", "PLAYER {0}" }, { "ready", "READY" }, { "ready_on", "READY!" },
        { "color_red", "Red" }, { "color_blue", "Blue" }, { "color_yellow", "Yellow" }, { "color_green", "Green" }, { "color_purple", "Purple" }, { "color_orange", "Orange" },
        // map select
        { "map_title", "SELECT COURSE" },
        { "course_space", "NEBULA RING" }, { "course_space_desc", "Neon road floating in space: boost pads, jump gaps, asteroids." },
        { "chip_space_len", "~1.3 KM LAP" }, { "chip_space", "SPACE" },
        { "course_forest", "FOREST SWITCHBACK" }, { "course_forest_desc", "Dirt road, hairpins and a slalom shortcut. One way through the forest." },
        { "course_circuit", "CPE ARENA CIRCUIT" }, { "course_circuit_desc", "White robot-arena road with a drift hairpin and fast sweepers." },
        { "chip_oneway", "ONE WAY" }, { "chip_forest_len", "~1.5 KM" }, { "chip_dirt", "DIRT" },
        { "chip_laps", "3 LAPS" }, { "chip_circuit_len", "~1 KM LAP" }, { "chip_arena", "ARENA" },
        // how to play
        { "howto_title", "HOW TO PLAY" },
        { "ctl_throttle", "Accelerate" }, { "ctl_brake", "Brake / reverse" }, { "ctl_steer", "Steer" },
        { "ctl_drift", "Drift (hold in turns)" }, { "ctl_respawn", "Respawn at checkpoint" },
        { "key_up", "UP" }, { "key_down", "DOWN" }, { "key_left", "LEFT" }, { "key_right", "RIGHT" },
        { "ctl_solo", "Single player: either set of keys works. ESC pauses the race." },
        { "rule_gems", "Collect gems into your truck bed: red = 1, blue = 2, gold = 3. The bed holds 12." },
        { "rule_drift", "Hold DRIFT while turning: 0.8 s = BOOST, 1.6 s = SUPER BOOST.\nDrifting over 2 s spills a gem." },
        { "rule_nitro", "Drifting also fills the NITRO tank. When it is full, double-tap your drift key\nto fire it (2.5 s at 1.45x speed)." },
        { "rule_crash", "Crashes drop gems: ramming a truck fast makes it spill 1-3 gems,\nand a hard wall hit spills 1. Anyone can pick them up." },
        { "rule_score", "Score = gem value in your bed + 5 for finishing first (-3 if you do not finish).\nHighest score wins." },
        // settings
        { "settings_title", "SETTINGS" }, { "set_music", "Music volume" }, { "set_sfx", "Sound effects volume" }, { "set_lang", "Language" },
        { "set_full", "Fullscreen" }, { "set_quality", "Graphics quality" }, { "set_minimap", "Show minimap" }, { "set_shake", "Camera shake" },
        { "on", "ON" }, { "off", "OFF" },
        { "quality_0", "Very Low" }, { "quality_1", "Low" }, { "quality_2", "Medium" }, { "quality_3", "High" }, { "quality_4", "Very High" }, { "quality_5", "Ultra" },
        // pause
        { "pause_title", "PAUSED" }, { "resume", "RESUME" }, { "restart", "RESTART" }, { "mainmenu", "MAIN MENU" },
    };

    static readonly Dictionary<string, string> Th = new Dictionary<string, string>
    {
        { "tagline", "หนึ่งจอ สองโชคชะตา" },
        { "play", "เล่น" }, { "howto", "วิธีเล่น" }, { "settings", "ตั้งค่า" }, { "quit", "ออกจากเกม" }, { "back", "ย้อนกลับ" },
        { "hint_move", "เลือก" }, { "hint_select", "ยืนยัน" }, { "hint_back", "ย้อนกลับ" }, { "hint_adjust", "ปรับ" },
        { "hint_choose", "เลือกสี" }, { "hint_car", "เลือกรถ" }, { "hint_ready", "พร้อม" },
        { "step_mode", "โหมด" }, { "step_racers", "ผู้แข่ง" }, { "step_color", "รถ" }, { "step_course", "สนาม" },
        { "mode_title", "เลือกโหมด" }, { "mode_single", "เล่นคนเดียว" }, { "mode_two", "2 ผู้เล่น" },
        { "mode_single_sub", "แข่งกับรถ AI" }, { "mode_two_sub", "แบ่งจอ ใช้คีย์บอร์ดเดียวกัน" },
        { "racers_title", "จำนวนผู้แข่งในแมตช์" }, { "racers_label", "รถบนกริดสตาร์ท" },
        { "racers_note", "ผู้เล่นรวมรถ AI 2-6 คัน รถ AI จะเติมที่ว่าง" },
        { "players_chip", "ผู้เล่น {0}" }, { "ai_chip", "รถ AI {0}" }, { "continue", "ต่อไป" },
        { "color_title", "เลือกรถของคุณ" },
        { "stat_speed", "ความเร็ว" }, { "stat_accel", "อัตราเร่ง" }, { "stat_handling", "การควบคุม" }, { "stat_gems", "ที่เก็บเพชร" },
        { "car_class_super", "ซูเปอร์คาร์" }, { "car_class_gt", "สปอร์ต GT" }, { "car_class_formula", "รถสูตรหนึ่ง" },
        { "car_class_hatch", "ฮอตแฮทช์" }, { "car_class_truck", "รถกระบะขนเพชร" }, { "player_n", "ผู้เล่น {0}" }, { "ready", "พร้อม" }, { "ready_on", "พร้อมแล้ว!" },
        { "color_red", "แดง" }, { "color_blue", "น้ำเงิน" }, { "color_yellow", "เหลือง" }, { "color_green", "เขียว" }, { "color_purple", "ม่วง" }, { "color_orange", "ส้ม" },
        { "map_title", "เลือกสนาม" },
        { "course_space", "เนบิวลา ริง" }, { "course_space_desc", "ถนนนีออนลอยกลางอวกาศ มีแผ่นบูสต์ ทางกระโดด อุกกาบาต" },
        { "chip_space_len", "รอบละ ~1.3 กม." }, { "chip_space", "อวกาศ" },
        { "course_forest", "ฟอเรสต์ สวิตช์แบ็ก" }, { "course_forest_desc", "ถนนดิน โค้งหักศอก และทางลัดสลาลม วิ่งทางเดียวผ่านป่า" },
        { "course_circuit", "ซีพีอี อารีนา เซอร์กิต" }, { "course_circuit_desc", "ถนนสีขาวสไตล์สนามหุ่นยนต์ มีโค้งหักศอกให้ดริฟต์และโค้งกว้างความเร็วสูง" },
        { "chip_oneway", "ทางเดียว" }, { "chip_forest_len", "~1.5 กม." }, { "chip_dirt", "ถนนดิน" },
        { "chip_laps", "3 รอบ" }, { "chip_circuit_len", "รอบละ ~1 กม." }, { "chip_arena", "สนามอารีนา" },
        { "howto_title", "วิธีเล่น" },
        { "ctl_throttle", "เร่งความเร็ว" }, { "ctl_brake", "เบรก / ถอยหลัง" }, { "ctl_steer", "เลี้ยว" },
        { "ctl_drift", "ดริฟต์ (กดค้างตอนเลี้ยว)" }, { "ctl_respawn", "กลับจุดเช็กพอยต์" },
        { "key_up", "ขึ้น" }, { "key_down", "ลง" }, { "key_left", "ซ้าย" }, { "key_right", "ขวา" },
        { "ctl_solo", "โหมดเล่นคนเดียว: ใช้ปุ่มชุดไหนก็ได้ กด ESC เพื่อหยุดเกม" },
        { "rule_gems", "เก็บอัญมณีใส่กระบะท้ายรถ: แดง = 1, น้ำเงิน = 2, ทอง = 3 กระบะจุได้ 12 เม็ด" },
        { "rule_drift", "กดดริฟต์ค้างตอนเลี้ยว: 0.8 วินาที = บูสต์, 1.6 วินาที = ซูเปอร์บูสต์\nดริฟต์เกิน 2 วินาที อัญมณีจะหล่น 1 เม็ด" },
        { "rule_nitro", "การดริฟต์ทำให้ถังไนโตรเติมด้วย เมื่อเต็มให้กดปุ่มดริฟต์สองครั้งติดกัน\nเพื่อพุ่ง (2.5 วินาที ความเร็ว 1.45 เท่า)" },
        { "rule_crash", "ชนแล้วอัญมณีหล่น: พุ่งชนรถคันอื่นแรงๆ ทำให้เขาทำอัญมณีหล่น 1-3 เม็ด\nชนกำแพงแรงๆ หล่น 1 เม็ด ใครก็เก็บได้" },
        { "rule_score", "คะแนน = มูลค่าอัญมณีในกระบะ + 5 ถ้าถึงเส้นชัยเป็นที่ 1 (-3 ถ้าไม่ถึงเส้นชัย)\nคะแนนสูงสุดชนะ" },
        { "settings_title", "ตั้งค่า" }, { "set_music", "ระดับเสียงดนตรี" }, { "set_sfx", "ระดับเสียงเอฟเฟกต์" }, { "set_lang", "ภาษา" },
        { "set_full", "เต็มจอ" }, { "set_quality", "คุณภาพกราฟิก" }, { "set_minimap", "แสดงแผนที่ย่อ" }, { "set_shake", "กล้องสั่น" },
        { "on", "เปิด" }, { "off", "ปิด" },
        { "quality_0", "ต่ำมาก" }, { "quality_1", "ต่ำ" }, { "quality_2", "ปานกลาง" }, { "quality_3", "สูง" }, { "quality_4", "สูงมาก" }, { "quality_5", "สูงสุด" },
        { "pause_title", "หยุดเกมชั่วคราว" }, { "resume", "เล่นต่อ" }, { "restart", "เริ่มใหม่" }, { "mainmenu", "เมนูหลัก" },
    };

    static readonly Dictionary<string, string>[] Tables = { En, Th };

    public static string T(string key)
    {
        int lang = UnityEngine.Mathf.Clamp(GameSettings.Language, 0, Tables.Length - 1);
        if (Tables[lang].TryGetValue(key, out var s)) return s;
        return En.TryGetValue(key, out s) ? s : key;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}
