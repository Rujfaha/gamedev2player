#set document(title: "Gem Drift — Task Split")
#set page(paper: "a4", margin: (x: 1.8cm, y: 1.6cm), numbering: "1 / 1")
#set text(font: "Sarabun", size: 10.5pt, lang: "th")
#set par(leading: 0.7em)
#let green = rgb("#1f6f3f")
#let blue = rgb("#1d5fa8")
#show heading.where(level: 1): it => block(above: 1.2em, below: 0.6em, text(size: 14pt, fill: green, it))
#show heading.where(level: 2): it => block(above: 0.9em, below: 0.4em, text(size: 11.5pt, it))
#let todo = box(width: 9pt, height: 9pt, stroke: 0.8pt + black, radius: 1.5pt)
#let note(title, body, color: rgb("#e8f5ec"), stroke: green) = block(
  width: 100%, fill: color, stroke: (left: 3pt + stroke), inset: 9pt, radius: 2pt, [*#title* \ #body])

#align(center)[
  #text(size: 22pt, weight: "bold", fill: green)[Gem Drift — แบ่งงาน] \
  #text(size: 11pt)[เกมแข่งรถ 3D low-poly 2 ผู้เล่น · CPE102 Game Jam \#40 · ส่ง 12 พ.ย. 16:59:59 (เป้า 11 พ.ย.)]
]

#grid(columns: (1fr, 1fr), gutter: 10pt,
  block(fill: rgb("#e8f5ec"), inset: 10pt, radius: 4pt, width: 100%)[
    #text(size: 13pt, weight: "bold", fill: green)[🎨 คน A — Top (ดีไซน์ / 3D)] \
    *"โลกของเกมทั้งหมด"* \
    สนาม, ฉาก, อัญมณี (โมเดล), แสง, UI หน้าตา, เสียง, หน้า itch
  ],
  block(fill: rgb("#e6eef8"), inset: 10pt, radius: 4pt, width: 100%)[
    #text(size: 13pt, weight: "bold", fill: blue)[💻 คน B — เพื่อน (โปรแกรม)] \
    *"ทุกอย่างที่ขยับและนับคะแนน"* \
    รถ, ดริฟต์, อัญมณี (ระบบ), รอบ/เส้นชัย, คะแนน, กล้อง, build
  ],
)

= คน A — Top: ดีไซน์ / 3D
#table(columns: (auto, 1fr), inset: 6pt, stroke: 0.5pt + gray,
  fill: (_, y) => if y == 0 { rgb("#e8f5ec") },
  [*\#*], [*งาน*],
  [A1], [#todo ร่างสนามบนกระดาษ: จุดเริ่ม/เส้นชัย, โค้งดริฟต์ 3–4 จุด, *ทางแยก 1 จุด* (สั้น/อัญมณีน้อย vs ยาว/อัญมณีทอง)],
  [A2], [#todo ต่อสนามด้วย *Kenney Racing Kit* ใน scene `Track.unity` (เปิด grid snapping) → ทำเป็น prefab `Track`],
  [A3], [#todo ใส่ *มาร์กเกอร์* ให้โค้ดของ B ใช้ (ดูหัวข้อ "ข้อตกลงระหว่างกัน"): Checkpoint, จุดวางอัญมณี, จุดเริ่ม P1/P2],
  [A4], [#todo ตกแต่ง (ใช้โมเดลฟรีจาก Unity Asset Store ได้ทุกอย่าง — ไม่ต้องสร้างเอง แต่ *จดชื่อ asset + ผู้สร้าง* ไว้ใส่เครดิต)ให้นึกถึง CPE102: เส้นดำกลางถนน (line-tracing), ป้าย KMUTT/CPE102, กรวย, รั้ว, ฉากห้องแล็บ/ลานหุ่นยนต์],
  [A5], [#todo อัญมณี 3 สี (แดง/น้ำเงิน/ทอง) — *ไม่ต้องปั้นเอง* ดึงโมเดลฟรีจาก Unity Asset Store (ค้น "low poly gem/crystal") → prefab `Gem_Red/Blue/Gold` (ไม่ต้องมีโค้ด)],
  [A6], [#todo แสง + skybox + มุมกล้องที่สวย (ช่วยกันเลือกมุมกับ B)],
  [A7], [#todo ออกแบบ UI: Main Menu, How to Play, HUD (P1 ซ้ายบน / P2 ขวาบน), หน้าผลการแข่ง — วาง layout + ฟอนต์ + สี (B เป็นคนต่อโค้ดตัวเลข)],
  [A8], [#todo เสียง: BGM, เครื่องยนต์, ยางดริฟต์, เก็บอัญมณี, อัญมณีหล่น, นับถอยหลัง (CC0 / CC-BY) + จดเครดิตไว้],
  [A9], [#todo หน้า itch: icon/cover 630×500, screenshots 3–5 รูป, GIF ดริฟต์, คำอธิบายคอนเซปต์-กลไก-ปุ่ม-เครดิต],
)

= คน B — เพื่อน: โปรแกรม
#table(columns: (auto, 1fr), inset: 6pt, stroke: 0.5pt + gray,
  fill: (_, y) => if y == 0 { rgb("#e6eef8") },
  [*\#*], [*งาน*],
  [B1], [#todo ติดตั้ง package: *Input System*, *Cinemachine* · ตั้ง action map `Player1` (WASD + Left Shift) / `Player2` (ลูกศร + Right Shift)],
  [B2], [#todo ระบบรถใน scene `CarTest.unity` บนพื้นเรียบ (ใช้ Cube ก่อน): เร่ง, เบรก/ถอย, เลี้ยว],
  [B3], [#todo *ดริฟต์*: กด Shift ค้าง → ลดแรงยึดด้านข้าง, ควันล้อ · ดริฟต์ 0.8 วิ = Mini-Boost, 1.6 วิ = Super-Boost],
  [B4], [#todo เปลี่ยน Cube เป็นโมเดล `delivery-flat` (รถกระบะ Kenney Car Kit) 2 สี → prefab `Car_P1`, `Car_P2`],
  [B5], [#todo ระบบอัญมณี: เก็บเข้า *กระบะท้าย* (กองให้เห็น สูงสุด 12), ค่า 1/2/3, หล่นเมื่อชนแรง/ดริฟต์เกิน 2 วิ, หนักแล้วช้าลง (สูงสุด −15%)],
  [B6], [#todo `RaceManager`: นับถอยหลัง 3-2-1-GO, 3 รอบผ่าน checkpoint ตามลำดับ, เส้นชัย, คนที่ 2 มีเวลา 10 วิ, คิดคะแนน (อัญมณี + 5 คนเข้าที่ 1)],
  [B7], [#todo กล้องเดียวเห็นรถทั้งสองคัน (Cinemachine Target Group) + ถ้าห่างเกิน ให้คันหลังวาร์ปไป checkpoint ล่าสุด],
  [B8], [#todo ต่อ UI ของ A เข้ากับข้อมูลจริง (รอบ, อันดับ, คะแนน, แถบดริฟต์) + เปลี่ยน scene เมนู → แข่ง → ผล → เล่นใหม่],
  [B9], [#todo Build *WebGL* (Gzip + Decompression Fallback, 1280×720) อัปขึ้น itch แบบ draft ทดสอบ],
)

= ข้อตกลงระหว่างกัน (สำคัญ — ทำให้งานต่อกันได้)
#note([สิ่งที่ A วางในสนาม → B ใช้ในโค้ด])[
  - *Checkpoint:* GameObject เปล่า + `BoxCollider` (ติ๊ก Is Trigger) ชื่อ `CP_00`, `CP_01`, ... เรียงตามทางวิ่ง · `CP_00` = เส้นชัย · tag `Checkpoint`
  - *จุดวางอัญมณี:* GameObject เปล่าชื่อ `GemSpawn_Red_01`, `GemSpawn_Gold_01` ... (สีอยู่ในชื่อ) · tag `GemSpawn`
  - *จุดเริ่ม:* `Start_P1`, `Start_P2` (หันหน้าไปทางวิ่ง)
  - *ถนน* อยู่ layer `Road` · *กำแพง/รั้ว* อยู่ layer `Wall` (ต้องมี Collider)
]
#note([สิ่งที่ B ทำ → A ใช้], color: rgb("#e6eef8"), stroke: blue)[
  - prefab `Car_P1` / `Car_P2` ให้ A ลากไปลองขับในสนามได้ตั้งแต่วันที่ 6 (เพื่อปรับความกว้างถนน/โค้ง)
  - A ทำ prefab อัญมณีแค่ "หน้าตา" — B เพิ่ม script/collider เอง
]

= ตารางเวลา
#table(columns: (auto, 1fr, 1fr), inset: 5.5pt, stroke: 0.5pt + gray,
  fill: (_, y) => if y == 0 { rgb("#f2f2f2") },
  [*วัน*], [*🎨 A — Top*], [*💻 B — เพื่อน*],
  [พ. 4], [A1 ร่างสนาม · ลอง Racing Kit], [Clone repo · B1 package + input],
  [พฤ. 5], [A2 ต่อสนาม (blockout ทั้งเส้น)], [B2 รถวิ่ง/เลี้ยว (Cube)],
  [ศ. 6], [A3 มาร์กเกอร์ · A5 อัญมณี], [B3 ดริฟต์ + บูสต์ · B4 โมเดลรถ],
  [ส. 7], [A4 ตกแต่ง · A6 แสง], [B5 ระบบอัญมณี + กระบะ],
  [*อา. 8*], [*รวมงาน:* สนาม + รถ ในฉาก `Main.unity` — เล่นจบได้ 1 เกม], [B6 RaceManager · B7 กล้อง],
  [จ. 9], [A7 UI · A8 เสียง], [B8 ต่อ UI · B9 WebGL draft],
  [อ. 10], [A9 หน้า itch + screenshots], [แก้บั๊ก + ปรับสมดุล],
  [พ. 11], [*ส่งงาน* + ตรวจ checklist], [*ส่งงาน* + ทดสอบบน itch],
  [พฤ. 12], [สำรอง (ปิด 16:59:59)], [สำรอง],
)
*ทุกวัน:* เล่นทดสอบด้วยกัน 10 นาที (เกม 2 คน ต้องทดสอบ 2 คน) · ถ้าวันที่ 8 ยังเล่นจบไม่ได้ → ตัดทางแยก / Super-Boost / สีอัญมณีทิ้งก่อน

= กติกา GitHub (กันไฟล์ชน)
#grid(columns: (1fr, 1fr), gutter: 10pt,
  [
    *ใครเป็นเจ้าของโฟลเดอร์ไหน*
    - A: `Assets/_Track/`, `Assets/_Art/`, `Assets/_UI/`, `Assets/_Audio/`, scene `Track.unity`
    - B: `Assets/_Scripts/`, `Assets/_Cars/`, scene `CarTest.unity`
    - `Main.unity`: *แก้ทีละคน* — บอกในแชทก่อนแก้ทุกครั้ง
  ],
  [
    *ขั้นตอน*
    - แต่ละคนมี branch: `design` (A) / `gameplay` (B) → merge เข้า `main` เมื่อเสร็จแต่ละชิ้น
    - `git pull` ก่อนเริ่มงานทุกครั้ง, commit บ่อย ๆ
    - ใช้ Unity *6000.0.85f1* ทั้งคู่ · มี `.gitignore` ของ Unity (ไม่ push `Library/`)
    - ไม่ใช้ Git LFS (ไฟล์เล็ก) — ห้าม push ไฟล์เกิน 50 MB
  ],
)
