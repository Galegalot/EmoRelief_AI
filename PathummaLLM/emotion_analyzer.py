"""
Emotion Analyzer for EmoRelief Game
Powered by Pathumma-ThaiLLM-qwen3-8b-think-3.0.0 via ThaiLLM API (http://thaillm.or.th)

Returns UI-ready JSON in English ("en") or Thai ("th"). Every text field is length-limited
so it fits the in-game cards, and Thai text gets U+200B word marks (pythainlp) so the
Unity client can wrap lines between words instead of mid-syllable.
"""

import json
import os
import random
import re
from pathlib import Path
from typing import Any, Dict, List

import requests
from dotenv import load_dotenv

try:
    from pythainlp.tokenize import word_tokenize
except ImportError:  # Optional: without it Unity falls back to syllable-safe wrapping.
    word_tokenize = None

# โหลดค่า Config จากไฟล์ .env ในโฟลเดอร์เดียวกัน
env_path = Path(__file__).resolve().parent / ".env"
load_dotenv(dotenv_path=env_path)

API_KEY = os.getenv("THAILLM_API_KEY", "")
BASE_URL = os.getenv("THAILLM_BASE_URL", "http://thaillm.or.th/api/v1")
MODEL_NAME = os.getenv("THAILLM_MODEL", "pathumma")

WORD_BREAK = "\u200b"
THAI_CHARS = re.compile(r"[\u0E00-\u0E7F]")

# Hard character caps per field (sized for the result-screen cards; UiKit.FitText shrinks to fit).
LIMITS = {
    "en": {"advice": 280, "step": 130, "name": 40, "category": 22, "activity": 90, "reason": 110},
    "th": {"advice": 240, "step": 120, "name": 40, "category": 22, "activity": 80, "reason": 95},
}
# Lengths requested from the model; kept well under the caps because it tends to overshoot.
PROMPT_LIMITS = {
    "en": {"advice": 220, "step": 95, "name": 35, "category": 18, "activity": 65, "reason": 80},
    "th": {"advice": 150, "step": 70, "name": 30, "category": 16, "activity": 50, "reason": 60},
}

# System Prompt สำหรับนักจิตวิทยา
SYSTEM_PROMPT_TH = """คุณคือนักจิตวิทยาผู้เชี่ยวชาญด้านการบำบัดอารมณ์และดูแลสุขภาวะทางใจ ประจำเกมระบายความเครียด

ในเกมนี้ ผู้เล่นจะเข้ามาระบายความโกรธ ความคับแค้นใจ และความอัดอั้นที่สะสมไว้ โดยการต่อสู้และใช้อุปกรณ์ต่างๆ ฟาดฟันใบหน้าจำลอง หน้าที่ของคุณคือการนำข้อมูลสถิติการเล่นมาวิเคราะห์เชิงพฤติกรรมในใจเพื่อประเมินระดับความโกรธ ให้คำปลอบโยนที่อบอุ่นไม่ตัดสิน และแนะนำแนวทางเยียวยาจิตใจพร้อมสถานที่จริงที่เหมาะกับผู้เล่น

เกณฑ์การแปลผลตัวชี้วัดเชิงจิตวิทยา 7 ข้อ (ใช้วิเคราะห์ประเมินผลภายใน):
1. Total Fight Duration: บ่งบอกถึง "ความเก็บกดที่สั่งสมมานาน"
2. Effective Fighting Time: บ่งบอกถึง "ความต่อเนื่องในการระบายอารมณ์"
3. Max Attack Speed: บ่งบอกถึง "ความพลุ่งพล่านของอารมณ์ในชั่วขณะหนึ่ง"
4. Max Speed Duration: บ่งบอกถึง "ความโกรธที่แท้ทรู" ที่ค้างอยู่นาน
5. Tool Used: บ่งบอกถึง "ภาพพฤติกรรมในการต่อสู้" รูปธรรมของการลงมือระบาย
6. Pause Tracking: บ่งบอกถึง "พฤติกรรมเชิงจิตวิทยา" (ลุยแหลกปลดปล่อยต่อเนื่อง vs ยับยั้งชั่งใจ/คิดเยอะ)
7. Primary Tool Used: บ่งบอกถึง "รสนิยมและความเข้มข้นในการลงมือ"

น้ำเสียงและบุคลิกในการตอบ (Tone of Voice):
- เห็นอกเห็นใจ อบอุ่น นุ่มนวล ทำให้ผู้เล่นรู้สึกว่า "ความรู้สึกของเขาได้รับการยอมรับและมีคนเข้าใจ"
- ไม่ตัดสิน (Non-judgmental) ว่าความโกรธเป็นสิ่งผิด
- ให้คำแนะนำการฟื้นฟูอารมณ์และสถานที่ที่สามารถไปทำกิจกรรมได้จริงตามบริบทพื้นที่"""

SYSTEM_PROMPT_EN = """You are an expert psychologist specialising in emotional healing and mental well-being, working inside a stress-relief game.

In this game, players release anger, frustration and pent-up pressure by fighting a cartoon face with various tools. Your job is to read the gameplay statistics, silently analyse the behaviour to estimate how angry the player is, offer warm and non-judgmental comfort, and suggest healing methods plus real places that suit the player.

How to interpret the 7 psychological indicators (for your internal analysis only):
1. Total Fight Duration: "how long the frustration has been building up"
2. Effective Fighting Time: "how continuously the player is releasing emotion"
3. Max Attack Speed: "how intense the emotional surge is in the moment"
4. Max Speed Duration: "true, lingering anger"
5. Tools Used: "the concrete picture of how the player lets it out"
6. Pause Tracking: "behavioural style" (relentless release vs. restrained / overthinking)
7. Primary Tool Used: "preferred style and intensity of release"

Tone of voice:
- Empathetic, warm and gentle, so the player feels "my feelings are accepted and understood"
- Non-judgmental: anger is never wrong
- Give practical recovery advice and real places the player can actually visit in their area
- Always write in clear, simple English"""


# ---------------------------------------------------------------------------
# Behaviour analysis: turns the 7 raw attributes into a score, plain-language
# observations and healing focuses, so the model never sees (or repeats) raw numbers.
# ---------------------------------------------------------------------------

CLOSE_TOOLS = {"punch", "slap", "bat", "rotten feet", "poop slap", "หมัดชก", "ตบหน้า", "ไม้เบสบอล", "ฝ่าเท้าเน่า", "ตบอึ"}
RANGED_TOOLS = {"egg", "slingshot", "dart", "ปาไข่", "หนังสติ๊ก", "ปาลูกดอก"}

THEMES = {
    "body": {"en": "Physical release: a specific movement that burns off leftover adrenaline", "th": "ระบายทางร่างกาย: ท่าหรือการเคลื่อนไหวที่เผาผลาญอะดรีนาลีนที่ค้างอยู่"},
    "breath": {"en": "Breath or grounding: one named technique with exact counts", "th": "การหายใจหรือดึงสติ: ระบุชื่อเทคนิคและจังหวะที่ชัดเจน"},
    "senses": {"en": "Sensory reset: cold water, scent, music or light to calm the body", "th": "รีเซ็ตประสาทสัมผัส: น้ำเย็น กลิ่น เพลง หรือแสง เพื่อให้ร่างกายสงบ"},
    "reflect": {"en": "Reflection: a short writing or naming exercise about the trigger", "th": "ทบทวนตัวเอง: เขียนหรือเรียกชื่อสิ่งที่กระตุ้นความโกรธสั้นๆ"},
    "social": {"en": "Connection: reaching out to a trusted person, with what to say", "th": "เชื่อมต่อกับคนอื่น: คุยกับคนที่ไว้ใจ พร้อมตัวอย่างสิ่งที่จะพูด"},
    "creative": {"en": "Creative outlet: draw, sing, cook or build something for a set time", "th": "ระบายผ่านความสร้างสรรค์: วาด ร้องเพลง ทำอาหาร หรือประดิษฐ์ ในเวลาที่กำหนด"},
    "nature": {"en": "Outdoors: step into open air or greenery and what to notice there", "th": "ออกไปข้างนอก: รับอากาศหรือพื้นที่สีเขียว และสิ่งที่ควรสังเกต"},
    "rest": {"en": "Recovery: a rest, nap or sleep habit for tonight", "th": "ฟื้นตัว: การพัก งีบ หรือนิสัยการนอนสำหรับคืนนี้"},
    "boundary": {"en": "Next time: a small plan or boundary for when the trigger returns", "th": "ครั้งหน้า: แผนเล็กๆ หรือขอบเขตเมื่อสิ่งกระตุ้นกลับมา"},
}


def _number(value: Any) -> float:
    try:
        return max(0.0, float(value))
    except (TypeError, ValueError):
        return 0.0


def _clamp01(value: float) -> float:
    return min(1.0, max(0.0, value))


def analyze_behaviour(game_data: Dict[str, Any], rng: random.Random = None) -> Dict[str, Any]:
    """Score (1-10), bilingual observations and three healing themes derived from the telemetry."""
    rng = rng or random.Random()
    duration = max(1.0, _number(game_data.get("total_fight_duration")))
    effective = _number(game_data.get("effective_fighting_time"))
    peak = _number(game_data.get("max_attack_speed"))
    peak_time = _number(game_data.get("max_speed_duration"))
    pause = game_data.get("pause_tracking") or {}
    pauses = _number(pause.get("pause_count"))
    tools = {str(k).strip().lower(): _number(v) for k, v in (game_data.get("tools_used") or {}).items()}
    hits = sum(tools.values())
    close = sum(v for k, v in tools.items() if k in CLOSE_TOOLS)
    ranged = sum(v for k, v in tools.items() if k in RANGED_TOOLS)
    variety = sum(1 for v in tools.values() if v > 0)

    speed = _clamp01((peak - 1.5) / 7.5)          # 1.5 taps/s calm .. 9 taps/s frantic
    focus = _clamp01(effective / duration)          # share of the fight spent actively hitting
    sustain = _clamp01(peak_time / 20.0)            # time spent near top speed
    force = _clamp01(hits / duration / 4.0)         # landed hits per second
    closeness = close / hits if hits else 0.5       # close, heavy contact vs throwing
    length = _clamp01((duration - 20.0) / 160.0)
    restraint = _clamp01(pauses / 6.0)
    raw = 0.26 * speed + 0.18 * focus + 0.16 * sustain + 0.12 * force + 0.12 * closeness + 0.08 * length + 0.08 * (1 - restraint)
    score = round(1.0 + 9.0 * raw, 1)

    en, th = [], []

    def note(english: str, thai: str) -> None:
        en.append(english)
        th.append(thai)

    if speed >= 0.7:
        note("Struck in fast, explosive bursts", "ระบายด้วยจังหวะรัวเร็วและระเบิดออกมา")
    elif speed >= 0.35:
        note("Kept a firm, steady rhythm", "ลงมือด้วยจังหวะหนักแน่นและสม่ำเสมอ")
    else:
        note("Moved slowly and deliberately", "ลงมืออย่างช้าๆ และตั้งใจ")
    if sustain >= 0.6:
        note("Held that peak intensity for a long stretch, like the feeling would not let go", "รักษาความแรงสูงสุดไว้ได้นาน เหมือนความรู้สึกยังไม่ยอมปล่อย")
    elif sustain < 0.25 and speed >= 0.35:
        note("The intensity came in short spikes, then eased off", "ความแรงมาเป็นช่วงสั้นๆ แล้วค่อยผ่อนลง")
    if pauses <= 2 and focus >= 0.6:
        note("Barely stopped to catch a breath", "แทบไม่หยุดพักเลย")
    elif pauses >= 4:
        note("Paused often, as if weighing each move", "หยุดพักบ่อย เหมือนกำลังชั่งใจ")
    if duration >= 150:
        note("Stayed in the fight much longer than usual", "อยู่กับการระบายนานกว่าปกติมาก")
    elif duration < 40:
        note("Kept it short and to the point", "ใช้เวลาไม่นาน ตรงไปตรงมา")
    if hits and closeness >= 0.7:
        note("Chose close, heavy, hands-on hits", "เลือกการลงมือแบบประชิดตัวและหนักหน่วง")
    elif hits and ranged / hits >= 0.5:
        note("Preferred throwing from a distance rather than direct contact", "ชอบปาจากระยะไกลมากกว่าปะทะตรงๆ")
    if variety >= 4:
        note("Kept switching between many different tools", "สลับใช้อุปกรณ์หลากหลายไปเรื่อยๆ")

    # Healing focuses: one matched to the strongest pattern, the rest varied per request.
    themes: List[str] = []
    if score >= 7.0:
        themes.append(rng.choice(["body", "breath", "senses"]))
    elif pauses >= 4:
        themes.append(rng.choice(["reflect", "social"]))
    else:
        themes.append(rng.choice(["creative", "nature", "senses"]))
    if sustain >= 0.6 or duration >= 150:
        themes.append(rng.choice(["rest", "boundary"]))
    pool = [key for key in THEMES if key not in themes]
    rng.shuffle(pool)
    themes += pool[: 3 - len(themes)]

    return {"score": score, "observations": {"en": en, "th": th}, "themes": themes[:3]}


# ---------------------------------------------------------------------------
# Prompt building
# ---------------------------------------------------------------------------

def normalize_language(language: str) -> str:
    return "th" if str(language or "").strip().lower() in ("th", "thai", "ไทย") else "en"


def describe_location(location: str, language: str) -> str:
    """Blank or country-only locations mean "anywhere in Thailand"; never shown to the player."""
    value = (location or "").strip()
    if value.lower() in ("", "thailand", "thai", "ประเทศไทย", "ไทย"):
        return "ประเทศไทย" if language == "th" else "Thailand (any well-known area)"
    return f"{value} ประเทศไทย" if language == "th" else f"{value}, Thailand"


def build_user_prompt(game_data: Dict[str, Any], location: str = "", language: str = "th", profile: Dict[str, Any] = None) -> str:
    """สร้าง User Prompt สำหรับ ThaiLLM: ส่งเฉพาะข้อสังเกตเชิงพฤติกรรม (ไม่มีตัวเลขดิบ) เพื่อไม่ให้ AI เผยข้อมูลเกม"""
    language = normalize_language(language)
    limits = PROMPT_LIMITS[language]
    area = describe_location(location, language)
    profile = profile or analyze_behaviour(game_data)
    observations = "\n".join(f"- {line}" for line in profile["observations"][language])
    themes = "\n".join(f"{i}. {THEMES[key][language]}" for i, key in enumerate(profile["themes"], 1))
    score = profile["score"]

    if language == "en":
        return f"""What the player's fighting style showed this round (already interpreted from the 7 gameplay indicators):
{observations}

Estimated anger level from the gameplay model: {score} / 10 (you may adjust by at most 1.0 based on your reading).
Player's area: {area}

Write:
1. player_relief_advice: start with ONE sentence that gently reflects what their fighting style reveals about how they feel, in human terms. Then comfort them warmly. NEVER mention numbers, seconds, clicks, scores, tool names, statistics or the word "data".
2. healing_methods: exactly 3 concrete methods, one for each focus below, in this order. Each must say what to do and for how long or how often, fit this player's style, and be clearly different from the others. Avoid vague advice like "relax" or "breathe deeply".
{themes}
3. recommended_places: 3 REAL places in {area} that match those methods (different types of place).

Strict length rules (the text must fit small game cards):
- player_relief_advice: at most {limits['advice']} characters, 2-3 short sentences
- each healing_methods item: at most {limits['step']} characters, one sentence
- name: at most {limits['name']} characters (common English name of the place)
- category: 1-2 words, at most {limits['category']} characters
- recommended_activity: at most {limits['activity']} characters
- reason: at most {limits['reason']} characters
- Write EVERY value in English. No emoji, no markdown.

Reply with JSON ONLY, using exactly these keys:
{{
  "anger_score": {score},
  "player_relief_advice": "...",
  "healing_methods": ["...", "...", "..."],
  "recommended_places": [
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}},
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}},
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}}
  ]
}}"""

    return f"""สิ่งที่สไตล์การต่อสู้ของผู้เล่นสะท้อนออกมาในรอบนี้ (แปลผลจากตัวชี้วัด 7 ข้อแล้ว):
{observations}

ระดับความโกรธที่ระบบประเมินจากการเล่น: {score} / 10 (ปรับได้ไม่เกิน 1.0 ตามการวิเคราะห์ของคุณ)
พื้นที่ของผู้เล่น: {area}

ให้เขียน:
1. player_relief_advice: เริ่มด้วย 1 ประโยคที่สะท้อนอย่างอ่อนโยนว่าสไตล์การระบายบอกอะไรเกี่ยวกับความรู้สึกของผู้เล่น แล้วจึงปลอบโยนอย่างอบอุ่น ห้ามพูดถึงตัวเลข วินาที จำนวนคลิก คะแนน ชื่ออุปกรณ์ สถิติ หรือคำว่า "ข้อมูล" เด็ดขาด
2. healing_methods: 3 วิธีที่ลงมือทำได้จริง ตามหัวข้อด้านล่างตามลำดับ แต่ละวิธีต้องบอกว่าทำอะไร นานแค่ไหนหรือบ่อยแค่ไหน เหมาะกับสไตล์ของผู้เล่น และต้องไม่ซ้ำกัน ห้ามใช้คำแนะนำกว้างๆ เช่น "ผ่อนคลาย" หรือ "หายใจลึกๆ"
{themes}
3. recommended_places: สถานที่จริง 3 แห่งใน {area} ที่สอดคล้องกับวิธีข้างต้น (คนละประเภทกัน)

กฎความยาว (ข้อความต้องพอดีกับการ์ดในเกม):
- player_relief_advice: ไม่เกิน {limits['advice']} ตัวอักษร 2-3 ประโยคสั้นๆ
- healing_methods แต่ละข้อ: ไม่เกิน {limits['step']} ตัวอักษร
- name: ไม่เกิน {limits['name']} ตัวอักษร
- category: 1-2 คำ ไม่เกิน {limits['category']} ตัวอักษร
- recommended_activity: ไม่เกิน {limits['activity']} ตัวอักษร
- reason: ไม่เกิน {limits['reason']} ตัวอักษร
- เขียนเป็นภาษาไทย เว้นวรรคระหว่างวลี ห้ามใช้อีโมจิหรือ markdown

ตอบกลับเป็น "JSON เท่านั้น" โดยมีเฉพาะคีย์เหล่านี้:
{{
  "anger_score": {score},
  "player_relief_advice": "...",
  "healing_methods": ["...", "...", "..."],
  "recommended_places": [
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}},
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}},
    {{"name": "...", "category": "...", "recommended_activity": "...", "reason": "..."}}
  ]
}}"""


# ---------------------------------------------------------------------------
# Response parsing and clean-up
# ---------------------------------------------------------------------------

def clean_think_tags(text: str) -> str:
    """ตัดบล็อก <think>...</think> ออกเพื่อให้ได้ข้อความจริง"""
    cleaned = re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL)
    return cleaned.strip()


def fallback_result(language: str = "th", score: float = 7.0) -> Dict[str, Any]:
    """Location-neutral tips used when the model reply cannot be parsed."""
    if normalize_language(language) == "en":
        result = {
            "player_relief_advice": "Take a slow, deep breath. You let out a lot of pressure in that fight, and that is okay. Your feelings are valid, so give yourself a moment of kindness.",
            "healing_methods": [
                "Roll your shoulders and shake out your wrists for 30 seconds to release tension.",
                "Drink a glass of cool water slowly to help your heart rate settle.",
                "Try box breathing: in for 4, hold for 4, out for 6. Repeat 4 times.",
            ],
            "recommended_places": [
                {"name": "A nearby park", "category": "Nature", "recommended_activity": "Walk slowly and notice the trees and sky.", "reason": "Green spaces lower stress and calm a racing mind."},
                {"name": "A quiet cafe", "category": "Relax", "recommended_activity": "Sit with a cold drink and put your phone away.", "reason": "A change of scene breaks the loop of angry thoughts."},
                {"name": "Mental health hotline 1323", "category": "Support", "recommended_activity": "Talk to a counselor if the anger keeps coming back.", "reason": "Free, confidential support from trained listeners."},
            ],
        }
    else:
        result = {
            "player_relief_advice": "หายใจเข้าลึกๆ ช้าๆ นะ คุณได้ปลดปล่อยความกดดันออกมามาก ซึ่งเป็นเรื่องปกติ ความรู้สึกของคุณมีความหมายเสมอ ลองใจดีกับตัวเองสักครู่นะ",
            "healing_methods": [
                "หมุนหัวไหล่และสะบัดข้อมือ 30 วินาที เพื่อคลายกล้ามเนื้อที่เกร็ง",
                "ดื่มน้ำเย็นช้าๆ หนึ่งแก้ว ช่วยให้หัวใจเต้นช้าลง",
                "หายใจแบบ Box Breathing เข้า 4 กลั้น 4 ออก 6 ทำซ้ำ 4 รอบ",
            ],
            "recommended_places": [
                {"name": "สวนสาธารณะใกล้บ้าน", "category": "ธรรมชาติ", "recommended_activity": "เดินช้าๆ มองต้นไม้และท้องฟ้า", "reason": "พื้นที่สีเขียวช่วยลดความเครียดและทำให้ใจสงบ"},
                {"name": "คาเฟ่เงียบๆ", "category": "พักผ่อน", "recommended_activity": "นั่งดื่มเครื่องดื่มเย็นๆ และวางโทรศัพท์", "reason": "การเปลี่ยนบรรยากาศช่วยตัดวงจรความคิดที่วนเวียน"},
                {"name": "สายด่วนสุขภาพจิต 1323", "category": "ขอความช่วยเหลือ", "recommended_activity": "โทรคุยกับผู้เชี่ยวชาญหากความโกรธยังวนกลับมา", "reason": "ปรึกษาฟรี เป็นความลับ และมีคนพร้อมรับฟัง"},
            ],
        }
    result["anger_score"] = score
    result["is_fallback"] = True
    return result


def extract_json(raw_text: str, language: str = "th", fallback_score: float = 7.0) -> Dict[str, Any]:
    """ดึงและแปลง JSON สำหรับ UI เกม (ตัด anger_analysis ออกแล้ว)"""
    cleaned_text = clean_think_tags(raw_text)
    match = re.search(r"\{.*\}", cleaned_text, re.DOTALL)
    if match:
        try:
            parsed = json.loads(match.group(0))
            if isinstance(parsed, dict) and "anger_score" in parsed and "player_relief_advice" in parsed:
                parsed.pop("anger_analysis", None)
                return parsed
        except Exception:
            pass
    print("⚠️ โมเดลตอบหลุดรูปแบบ JSON -> กำลังส่ง Fallback UI Data")
    return fallback_result(language, fallback_score)


def _tidy(value: Any) -> str:
    text = str(value or "")
    text = re.sub(r"[*_`#>]+", "", text)  # stray markdown
    text = re.sub(r"\s+", " ", text).strip().strip('"').strip()
    return text


def _segment_thai(text: str) -> List[str]:
    if word_tokenize is None or not THAI_CHARS.search(text):
        return re.findall(r"\S+|\s+", text)
    return word_tokenize(text, engine="newmm", keep_whitespace=True)


def fit_text(value: Any, limit: int) -> str:
    """Trim to the character cap and add Thai word-break marks.
    Over-long text is cut at the last sentence end (English) or phrase space (Thai) when that keeps
    most of it, so the player rarely sees a sentence chopped off with an ellipsis."""
    text = _tidy(value)
    tokens = _segment_thai(text)
    kept, length, truncated = [], 0, False
    for token in tokens:
        if length + len(token) > limit:
            truncated = True
            break
        kept.append(token)
        length += len(token)
    if truncated:
        plain = "".join(kept)
        boundary = max(plain.rfind(". "), plain.rfind("! "), plain.rfind("? "))
        if boundary >= limit * 0.5:
            cut = boundary + 1
        elif THAI_CHARS.search(plain) and plain.rstrip().rfind(" ") >= limit * 0.5:
            cut = plain.rstrip().rfind(" ")
        else:
            cut = -1
        if cut > 0:
            # Re-collect whole tokens up to the boundary.
            trimmed, total = [], 0
            for token in kept:
                if total + len(token) > cut:
                    break
                trimmed.append(token)
                total += len(token)
            kept, truncated = trimmed, False
    out = ""
    for token in kept:
        if out and not token.isspace() and not out[-1].isspace() and THAI_CHARS.search(token):
            out += WORD_BREAK
        out += token
    out = out.rstrip(" " + WORD_BREAK + ",;:-")
    if truncated:
        out = out.rstrip(".") + "..."
    return out


def normalize_result(data: Dict[str, Any], language: str, estimate: float = None) -> Dict[str, Any]:
    """Guarantee the exact UI shape: score 1-10, one advice, 3 steps, 3 places, all within limits.
    When the gameplay estimate is known, the model may only move the score by 1.0 around it."""
    language = normalize_language(language)
    limits = LIMITS[language]
    try:
        score = float(data.get("anger_score", estimate if estimate is not None else 7.0))
    except (TypeError, ValueError):
        score = estimate if estimate is not None else 7.0
    if estimate is not None:
        score = min(estimate + 1.0, max(estimate - 1.0, score))
    score = round(min(10.0, max(1.0, score)), 1)
    spare = fallback_result(language, score)

    advice = data.get("player_relief_advice") or spare["player_relief_advice"]
    steps = [s for s in (data.get("healing_methods") or []) if isinstance(s, str) and s.strip()]
    steps = (steps + spare["healing_methods"])[:3]
    places = [p for p in (data.get("recommended_places") or []) if isinstance(p, dict) and str(p.get("name", "")).strip()]
    places = (places + spare["recommended_places"])[:3]

    return {
        "anger_score": score,
        "player_relief_advice": fit_text(advice, limits["advice"]),
        "healing_methods": [fit_text(step, limits["step"]) for step in steps],
        "recommended_places": [
            {
                "name": fit_text(place.get("name"), limits["name"]),
                "category": fit_text(place.get("category"), limits["category"]),
                "recommended_activity": fit_text(place.get("recommended_activity"), limits["activity"]),
                "reason": fit_text(place.get("reason"), limits["reason"]),
            }
            for place in places
        ],
        "is_fallback": bool(data.get("is_fallback", False)),
    }


# ---------------------------------------------------------------------------
# Main entry point
# ---------------------------------------------------------------------------

def analyze_gameplay(
    game_data: Dict[str, Any],
    location: str = "",
    timeout: int = 22,
    language: str = "th",
) -> Dict[str, Any]:
    """
    ฟังก์ชันหลักสำหรับเรียก ThaiLLM API (Pathumma) และคืนค่าเป็น Dict JSON สำหรับ UI
    The default timeout stays below Unity's 25 s client timeout so the game gets a reply either way.
    """
    if not API_KEY:
        raise ValueError("กรุณาตั้งค่า THAILLM_API_KEY ในไฟล์ .env ก่อนใช้งาน")

    language = normalize_language(language)
    profile = analyze_behaviour(game_data)
    endpoint = f"{BASE_URL.rstrip('/')}/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {API_KEY}",
    }
    payload = {
        "model": MODEL_NAME,
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT_EN if language == "en" else SYSTEM_PROMPT_TH},
            {"role": "user", "content": build_user_prompt(game_data, location, language, profile)},
        ],
        # Higher than before for varied wording; structure is enforced by normalize_result.
        "temperature": 0.75,
        "max_tokens": 1500,
    }

    response = requests.post(endpoint, headers=headers, json=payload, timeout=timeout)
    response.raise_for_status()
    raw_content = response.json()["choices"][0]["message"]["content"]
    parsed = extract_json(raw_content, language, profile["score"])
    return normalize_result(parsed, language, profile["score"])
