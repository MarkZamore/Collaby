#!/usr/bin/env python3
"""
Генератор пояснительной записки к дипломному проекту
для веб-приложения Collaby.

Использование:
 python scripts/generate_diploma.py

Зависимости:
 pip install python-docx Pillow
"""

from __future__ import annotations

import os
import sys
from pathlib import Path
from io import BytesIO

from docx import Document
from docx.shared import Pt, Cm, Inches, Emu, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.section import WD_ORIENT
from docx.oxml.ns import qn, nsdecls
from docx.oxml import parse_xml
import docx.opc.constants

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
PROJECT_ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIR = PROJECT_ROOT / "output"
OUTPUT_FILE = OUTPUT_DIR / "diploma_collaby.docx"
SCREENSHOT_DIR = OUTPUT_DIR / "screenshots"

FONT_NAME = "Times New Roman"
FONT_SIZE = Pt(14)
CODE_FONT = "Courier New"
CODE_SIZE = Pt(10)
LINE_SPACING = Pt(14 * 1.0)  # single spacing at 14pt

LEFT_MARGIN = Cm(3.0)
RIGHT_MARGIN = Cm(1.0)
TOP_MARGIN = Cm(2.0)
BOTTOM_MARGIN = Cm(2.5)
FIRST_LINE_INDENT = Cm(1.25)

UNIVERSITY = "КАЗАХСКИЙ НАЦИОНАЛЬНЫЙ ИССЛЕДОВАТЕЛЬСКИЙ ТЕХНИЧЕСКИЙ УНИВЕРСИТЕТ имени К.И. САТПАЕВА"
UNIVERSITY_SHORT = "КазНИТУ им. К.И. Сатпаева"
DEPARTMENT = 'Кафедра "Программная инженерия"'
PROGRAM = "ОП 6B06102 - Computer Science"
YEAR = "2026"

# Counters
_figure_counters: dict[int, int] = {}
_table_counters: dict[int, int] = {}
_current_section: int = 0


def _next_figure(section: int | None = None) -> str:
    s = section if section is not None else _current_section
    _figure_counters.setdefault(s, 0)
    _figure_counters[s] += 1
    if s == 0:
        return str(_figure_counters[s])
    return f"{s}.{_figure_counters[s]}"


def _next_table(section: int | None = None) -> str:
    s = section if section is not None else _current_section
    _table_counters.setdefault(s, 0)
    _table_counters[s] += 1
    if s == 0:
        return str(_table_counters[s])
    return f"{s}.{_table_counters[s]}"


# ---------------------------------------------------------------------------
# Document setup helpers
# ---------------------------------------------------------------------------

def create_document() -> Document:
    doc = Document()

    # Page setup
    for section in doc.sections:
        section.page_width = Cm(21.0)
        section.page_height = Cm(29.7)
        section.left_margin = LEFT_MARGIN
        section.right_margin = RIGHT_MARGIN
        section.top_margin = TOP_MARGIN
        section.bottom_margin = BOTTOM_MARGIN

    _setup_styles(doc)
    return doc


def _setup_styles(doc: Document):
    style = doc.styles["Normal"]
    font = style.font
    font.name = FONT_NAME
    font.size = FONT_SIZE
    font.color.rgb = RGBColor(0, 0, 0)
    pf = style.paragraph_format
    pf.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    pf.space_before = Pt(0)
    pf.space_after = Pt(0)
    pf.line_spacing_rule = WD_LINE_SPACING.SINGLE
    pf.first_line_indent = FIRST_LINE_INDENT

    # Heading 1
    h1 = doc.styles["Heading 1"]
    h1.font.name = FONT_NAME
    h1.font.size = FONT_SIZE
    h1.font.bold = True
    h1.font.color.rgb = RGBColor(0, 0, 0)
    h1.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    h1.paragraph_format.space_before = Pt(0)
    h1.paragraph_format.space_after = Pt(28)
    h1.paragraph_format.page_break_before = True
    h1.paragraph_format.first_line_indent = Cm(0)
    h1.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
    # Remove numbering
    h1_element = h1.element
    pPr = h1_element.find(qn('w:pPr'))
    if pPr is None:
        pPr = parse_xml(f'<w:pPr {nsdecls("w")}/>')
        h1_element.insert(0, pPr)
    numPr = pPr.find(qn('w:numPr'))
    if numPr is not None:
        pPr.remove(numPr)

    # Heading 2
    h2 = doc.styles["Heading 2"]
    h2.font.name = FONT_NAME
    h2.font.size = FONT_SIZE
    h2.font.bold = True
    h2.font.color.rgb = RGBColor(0, 0, 0)
    h2.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.LEFT
    h2.paragraph_format.space_before = Pt(14)
    h2.paragraph_format.space_after = Pt(14)
    h2.paragraph_format.first_line_indent = FIRST_LINE_INDENT
    h2.paragraph_format.page_break_before = False
    h2.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
    h2_element = h2.element
    pPr2 = h2_element.find(qn('w:pPr'))
    if pPr2 is None:
        pPr2 = parse_xml(f'<w:pPr {nsdecls("w")}/>')
        h2_element.insert(0, pPr2)
    numPr2 = pPr2.find(qn('w:numPr'))
    if numPr2 is not None:
        pPr2.remove(numPr2)

    # Heading 3
    h3 = doc.styles["Heading 3"]
    h3.font.name = FONT_NAME
    h3.font.size = FONT_SIZE
    h3.font.bold = True
    h3.font.color.rgb = RGBColor(0, 0, 0)
    h3.paragraph_format.space_before = Pt(14)
    h3.paragraph_format.space_after = Pt(14)
    h3.paragraph_format.first_line_indent = FIRST_LINE_INDENT
    h3.paragraph_format.page_break_before = False
    h3.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE


def add_normal_paragraph(doc: Document, text: str, bold: bool = False,
                          alignment=None, indent: bool = True,
                          first_line_indent=None) -> None:
    p = doc.add_paragraph()
    p.style = doc.styles["Normal"]
    if alignment is not None:
        p.alignment = alignment
    if not indent:
        p.paragraph_format.first_line_indent = Cm(0)
    if first_line_indent is not None:
        p.paragraph_format.first_line_indent = first_line_indent
    run = p.add_run(text)
    run.font.name = FONT_NAME
    run.font.size = FONT_SIZE
    run.bold = bold


def add_empty_line(doc: Document, count: int = 1):
    for _ in range(count):
        p = doc.add_paragraph()
        p.style = doc.styles["Normal"]
        p.paragraph_format.first_line_indent = Cm(0)
        run = p.add_run("")
        run.font.size = FONT_SIZE


def add_centered_text(doc: Document, text: str, bold: bool = False, size=None):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    run = p.add_run(text)
    run.font.name = FONT_NAME
    run.font.size = size or FONT_SIZE
    run.bold = bold


def add_code_block(doc: Document, code: str, caption: str | None = None):
    """Add a code listing with optional caption."""
    if caption:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_before = Pt(6)
        run = p.add_run(caption)
        run.font.name = FONT_NAME
        run.font.size = Pt(12)
        run.italic = True

    for line in code.strip().split("\n"):
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(0)
        p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
        run = p.add_run(line)
        run.font.name = CODE_FONT
        run.font.size = CODE_SIZE


def add_simple_table(doc: Document, headers: list[str], rows: list[list[str]],
                      caption: str | None = None) -> None:
    """Add a plain table with optional caption above."""
    if caption:
        tbl_num = _next_table()
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_before = Pt(6)
        p.paragraph_format.space_after = Pt(3)
        run = p.add_run(f"Таблица {tbl_num} - {caption}")
        run.font.name = FONT_NAME
        run.font.size = Pt(12)
        run.bold = True

    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.style = "Table Grid"

    # Header row
    for i, h in enumerate(headers):
        cell = table.rows[0].cells[i]
        cell.text = ""
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        run = p.add_run(h)
        run.font.name = FONT_NAME
        run.font.size = Pt(12)
        run.bold = True

    # Data rows
    for ri, row in enumerate(rows):
        for ci, val in enumerate(row):
            cell = table.rows[ri + 1].cells[ci]
            cell.text = ""
            p = cell.paragraphs[0]
            p.paragraph_format.first_line_indent = Cm(0)
            run = p.add_run(val)
            run.font.name = FONT_NAME
            run.font.size = Pt(12)

    # Spacing after
    add_empty_line(doc)


def add_figure_placeholder(doc: Document, caption_text: str, screenshot_name: str | None = None):
    """Insert a screenshot if available, otherwise a placeholder."""
    fig_num = _next_figure()
    inserted = False
    if screenshot_name:
        img_path = SCREENSHOT_DIR / screenshot_name
        if img_path.exists():
            doc.add_picture(str(img_path), width=Cm(12))
            last_p = doc.paragraphs[-1]
            last_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            last_p.paragraph_format.first_line_indent = Cm(0)
            inserted = True

    if not inserted:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(6)
        run = p.add_run(f"[Скриншот: {caption_text}]")
        run.font.name = FONT_NAME
        run.font.size = Pt(12)
        run.italic = True

    # Caption below
    p_cap = doc.add_paragraph()
    p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_cap.paragraph_format.first_line_indent = Cm(0)
    p_cap.paragraph_format.space_before = Pt(3)
    p_cap.paragraph_format.space_after = Pt(12)
    run_cap = p_cap.add_run(f"Рисунок {fig_num} - {caption_text}")
    run_cap.font.name = FONT_NAME
    run_cap.font.size = Pt(12)


def add_toc(doc: Document):
    """Insert a Word TOC field."""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    run = p.add_run("СОДЕРЖАНИЕ")
    run.font.name = FONT_NAME
    run.font.size = FONT_SIZE
    run.bold = True

    add_empty_line(doc)

    paragraph = doc.add_paragraph()
    paragraph.paragraph_format.first_line_indent = Cm(0)
    run = paragraph.add_run()
    fldChar1 = parse_xml(f'<w:fldChar {nsdecls("w")} w:fldCharType="begin"/>')
    run._r.append(fldChar1)
    run2 = paragraph.add_run()
    instrText = parse_xml(f'<w:instrText {nsdecls("w")} xml:space="preserve"> TOC \\o "1-3" \\h \\z \\u </w:instrText>')
    run2._r.append(instrText)
    run3 = paragraph.add_run()
    fldChar2 = parse_xml(f'<w:fldChar {nsdecls("w")} w:fldCharType="separate"/>')
    run3._r.append(fldChar2)
    run4 = paragraph.add_run("(Обновите оглавление: выделите поле и нажмите F9)")
    run4.font.name = FONT_NAME
    run4.font.size = Pt(12)
    run4.font.color.rgb = RGBColor(128, 128, 128)
    run5 = paragraph.add_run()
    fldChar3 = parse_xml(f'<w:fldChar {nsdecls("w")} w:fldCharType="end"/>')
    run5._r.append(fldChar3)


def add_page_break(doc: Document):
    p = doc.add_paragraph()
    p.paragraph_format.first_line_indent = Cm(0)
    run = p.add_run()
    run.add_break(docx.enum.text.WD_BREAK.PAGE)


def _try_generate_architecture_diagram() -> bytes | None:
    """Generate a simple architecture box diagram using Pillow if available."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1200, 700
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)

    try:
        font = ImageFont.truetype("arial.ttf", 14)
        font_bold = ImageFont.truetype("arialbd.ttf", 16)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    boxes = [
        {"label": "Frontend\n(Caddy)", "x": 480, "y": 20, "w": 240, "h": 60, "color": "#4FC3F7"},
        {"label": "Access Host\n:5001\n(Identity + User)", "x": 60, "y": 180, "w": 220, "h": 80, "color": "#81C784"},
        {"label": "Workspace Host\n:5003\n(Management + Analytics)", "x": 320, "y": 180, "w": 240, "h": 80, "color": "#FFB74D"},
        {"label": "Realtime Host\n:5004\n(Messenger + Call)", "x": 600, "y": 180, "w": 230, "h": 80, "color": "#CE93D8"},
        {"label": "Engagement Host\n:5006\n(Gamification)", "x": 870, "y": 180, "w": 220, "h": 80, "color": "#EF5350"},
        {"label": "Assistant\n:8100\n(AI Agent)", "x": 870, "y": 320, "w": 220, "h": 80, "color": "#90A4AE"},
        {"label": "Apache Kafka\n(Event Bus)", "x": 350, "y": 330, "w": 200, "h": 60, "color": "#FFF176"},
        {"label": "PostgreSQL", "x": 60, "y": 460, "w": 160, "h": 50, "color": "#80CBC4"},
        {"label": "MongoDB", "x": 260, "y": 460, "w": 160, "h": 50, "color": "#A5D6A7"},
        {"label": "Redis", "x": 460, "y": 460, "w": 160, "h": 50, "color": "#EF9A9A"},
        {"label": "Grafana + Prometheus\n+ Loki + Tempo", "x": 700, "y": 460, "w": 230, "h": 50, "color": "#FFCC80"},
    ]

    for box in boxes:
        x, y, w, h = box["x"], box["y"], box["w"], box["h"]
        draw.rectangle([x, y, x + w, y + h], fill=box["color"], outline="black", width=2)
        # Center text
        lines = box["label"].split("\n")
        total_h = len(lines) * 18
        start_y = y + (h - total_h) // 2
        for i, line in enumerate(lines):
            bbox = draw.textbbox((0, 0), line, font=font)
            tw = bbox[2] - bbox[0]
            tx = x + (w - tw) // 2
            draw.text((tx, start_y + i * 18), line, fill="black", font=font)

    # Arrows from frontend to hosts
    for bx in [170, 440, 715, 980]:
        draw.line([(600, 80), (bx, 180)], fill="black", width=2)

    # Arrows from hosts to kafka
    for bx in [170, 440, 715, 980]:
        draw.line([(bx, 260 if bx != 980 else 260), (450, 330)], fill="gray", width=1)

    # Arrows from kafka to DBs
    for bx in [140, 340, 540]:
        draw.line([(450, 390), (bx, 460)], fill="gray", width=1)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_er_diagram_identity() -> bytes | None:
    """ER diagram for IdentityService database."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1100, 650
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 12)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    entities = [
        {"name": "User", "fields": ["Id: Guid (PK)", "Email: string", "Username: string", "PasswordHash: string",
                                      "Provider: AuthProvider", "IsEmailVerified: bool", "IsActive: bool",
                                      "CreatedAt: DateTime", "LastLoginAt: DateTime?"],
         "x": 400, "y": 20},
        {"name": "RefreshToken", "fields": ["Id: Guid (PK)", "Token: string", "UserId: Guid (FK)", "ExpiresAt: DateTime"],
         "x": 50, "y": 30},
        {"name": "OAuthAccount", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "Provider: string", "ProviderId: string", "Email: string"],
         "x": 50, "y": 220},
        {"name": "TwoFactorSetting", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "IsEnabled: bool", "Secret: string"],
         "x": 50, "y": 400},
        {"name": "EmailVerification", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "Token: string", "ExpiresAt: DateTime"],
         "x": 750, "y": 30},
        {"name": "PasswordReset", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "Token: string", "ExpiresAt: DateTime"],
         "x": 750, "y": 220},
        {"name": "UserDeletionOp", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "Status: string", "CorrelationId: string"],
         "x": 750, "y": 400},
    ]

    for ent in entities:
        x, y = ent["x"], ent["y"]
        w = 300
        h = 15 + len(ent["fields"]) * 15 + 10
        draw.rectangle([x, y, x + w, y + h], fill="#E3F2FD", outline="black", width=1)
        draw.rectangle([x, y, x + w, y + 20], fill="#1565C0", outline="black", width=1)
        bbox = draw.textbbox((0, 0), ent["name"], font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (w - tw) // 2, y + 3), ent["name"], fill="white", font=font_bold)
        for i, field in enumerate(ent["fields"]):
            draw.text((x + 5, y + 22 + i * 15), field, fill="black", font=font)

    # Draw relationship lines from child entities to User
    user_center = (550, 120)
    for ent in entities:
        if ent["name"] != "User":
            ex = ent["x"] + 150
            ey = ent["y"] + 10
            draw.line([ex, ey, user_center[0], user_center[1]], fill="#666666", width=1)

    draw.text((10, H - 20), "collaby_identity (PostgreSQL)", fill="#1565C0", font=font_bold)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_er_diagram_management() -> bytes | None:
    """ER diagram for ManagementService database."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1200, 750
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 12)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    entities = [
        {"name": "Workspace", "fields": ["Id: Guid (PK)", "Name: string", "Description: string",
                                           "OwnerId: Guid", "IsArchived: bool"],
         "x": 20, "y": 20},
        {"name": "WorkspaceMember", "fields": ["Id: Guid (PK)", "WorkspaceId: Guid (FK)", "UserId: Guid",
                                                 "Role: string", "JoinedAt: DateTime"],
         "x": 20, "y": 200},
        {"name": "Project", "fields": ["Id: Guid (PK)", "WorkspaceId: Guid (FK)", "Name: string",
                                         "CreatedBy: Guid", "IsArchived: bool"],
         "x": 400, "y": 20},
        {"name": "ProjectStatus", "fields": ["Id: Guid (PK)", "ProjectId: Guid (FK)", "Name: string",
                                               "Order: int", "Color: string", "IsFinal: bool"],
         "x": 400, "y": 200},
        {"name": "TaskItem", "fields": ["Id: Guid (PK)", "ProjectId: Guid (FK)", "StatusId: Guid (FK)",
                                          "Title: string", "AssigneeId: Guid?", "Priority: enum",
                                          "Order: int", "DueDate: DateTime?"],
         "x": 750, "y": 20},
        {"name": "SubTaskItem", "fields": ["Id: Guid (PK)", "TaskId: Guid (FK)", "Title: string",
                                             "IsCompleted: bool", "Order: int"],
         "x": 750, "y": 230},
        {"name": "TaskComment", "fields": ["Id: Guid (PK)", "TaskId: Guid (FK)", "UserId: Guid",
                                             "Text: string", "CreatedAt: DateTime"],
         "x": 750, "y": 400},
        {"name": "Notification", "fields": ["Id: Guid (PK)", "UserId: Guid", "Type: enum",
                                              "Title: string", "IsRead: bool"],
         "x": 400, "y": 400},
        {"name": "WorkspaceInvitation", "fields": ["Id: Guid (PK)", "WorkspaceId: Guid (FK)",
                                                     "Email: string", "Token: string", "Status: string"],
         "x": 20, "y": 400},
        {"name": "ProjectLabel", "fields": ["Id: Guid (PK)", "ProjectId: Guid (FK)", "Name: string", "Color: string"],
         "x": 400, "y": 580},
    ]

    for ent in entities:
        x, y = ent["x"], ent["y"]
        w = 300
        h = 15 + len(ent["fields"]) * 15 + 10
        draw.rectangle([x, y, x + w, y + h], fill="#FFF3E0", outline="black", width=1)
        draw.rectangle([x, y, x + w, y + 20], fill="#E65100", outline="black", width=1)
        bbox = draw.textbbox((0, 0), ent["name"], font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (w - tw) // 2, y + 3), ent["name"], fill="white", font=font_bold)
        for i, field in enumerate(ent["fields"]):
            draw.text((x + 5, y + 22 + i * 15), field, fill="black", font=font)

    # Relationships
    draw.line([(170, 140), (170, 200)], fill="#333", width=1)  # Workspace -> Member
    draw.line([(320, 70), (400, 70)], fill="#333", width=1)  # Workspace -> Project
    draw.line([(550, 140), (550, 200)], fill="#333", width=1)  # Project -> Status
    draw.line([(700, 70), (750, 70)], fill="#333", width=1)  # Project -> Task
    draw.line([(900, 160), (900, 230)], fill="#333", width=1)  # Task -> SubTask
    draw.line([(900, 160), (900, 400)], fill="#333", width=1)  # Task -> Comment
    draw.line([(550, 140), (550, 580)], fill="#333", width=1)  # Project -> Label

    draw.text((10, H - 20), "collaby_management (PostgreSQL)", fill="#E65100", font=font_bold)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_er_diagram_gamification() -> bytes | None:
    """ER diagram for GamificationService database."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1200, 750
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 12)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    entities = [
        {"name": "UserGlobalXp", "fields": ["UserId: Guid (PK)", "TotalXp: long", "Level: int",
                                              "Coins: long", "StreakDays: int", "LastActiveDate: DateTime?",
                                              "FreezeCount: int"],
         "x": 20, "y": 20},
        {"name": "UserProjectXp", "fields": ["UserId: Guid (PK)", "ProjectId: Guid (PK)", "Xp: long",
                                               "Level: int", "TasksCompleted: int"],
         "x": 20, "y": 220},
        {"name": "XpTransaction", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "ProjectId: Guid?",
                                               "Amount: long", "Reason: string", "Status: string",
                                               "CreatedAt: DateTime"],
         "x": 350, "y": 20},
        {"name": "Quest", "fields": ["Id: Guid (PK)", "Title: string", "Description: string",
                                       "Type: string", "Reward: int"],
         "x": 700, "y": 20},
        {"name": "UserQuest", "fields": ["UserId: Guid (PK)", "QuestId: Guid (PK/FK)", "WindowKey: string",
                                           "Progress: int", "CompletedAt: DateTime?"],
         "x": 700, "y": 200},
        {"name": "ShopItem", "fields": ["Id: Guid (PK)", "Name: string", "Category: string",
                                          "Cost: int", "IsPurchasable: bool"],
         "x": 350, "y": 280},
        {"name": "UserInventory", "fields": ["Id: Guid (PK)", "UserId: Guid (FK)", "ShopItemId: Guid (FK)",
                                               "PurchasedAt: DateTime"],
         "x": 350, "y": 460},
        {"name": "LeaderboardSnapshot", "fields": ["Id: Guid (PK)", "Type: string", "ProjectId: Guid?",
                                                      "Rank: int", "UserId: Guid", "Score: long"],
         "x": 20, "y": 420},
        {"name": "SkillTreeNode", "fields": ["Id: Guid (PK)", "ProjectId: Guid", "Name: string",
                                               "Tier: int", "Prerequisites: JSON"],
         "x": 700, "y": 400},
        {"name": "UserSkillTreeChoice", "fields": ["UserId: Guid (PK)", "SkillNodeId: Guid (PK/FK)",
                                                      "SelectedAt: DateTime"],
         "x": 700, "y": 580},
    ]

    for ent in entities:
        x, y = ent["x"], ent["y"]
        w = 310
        h = 15 + len(ent["fields"]) * 15 + 10
        draw.rectangle([x, y, x + w, y + h], fill="#FCE4EC", outline="black", width=1)
        draw.rectangle([x, y, x + w, y + 20], fill="#C62828", outline="black", width=1)
        bbox = draw.textbbox((0, 0), ent["name"], font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (w - tw) // 2, y + 3), ent["name"], fill="white", font=font_bold)
        for i, field in enumerate(ent["fields"]):
            draw.text((x + 5, y + 22 + i * 15), field, fill="black", font=font)

    draw.line([(330, 100), (350, 100)], fill="#333", width=1)
    draw.line([(1000, 140), (1000, 200)], fill="#333", width=1)
    draw.line([(660, 350), (660, 460)], fill="#333", width=1)
    draw.line([(1000, 520), (1000, 580)], fill="#333", width=1)

    draw.text((10, H - 20), "collaby_gamification (PostgreSQL)", fill="#C62828", font=font_bold)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_event_flow_diagram() -> bytes | None:
    """Kafka event flow diagram."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1200, 600
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 13)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    # Producers (left)
    producers = [
        ("IdentityService", 50, 30, "#81C784"),
        ("UserService", 50, 110, "#A5D6A7"),
        ("ManagementService", 50, 190, "#FFB74D"),
        ("GamificationService", 50, 310, "#EF5350"),
    ]

    # Topics (center)
    topics = [
        "identity.user-lifecycle.v1",
        "identity.user-directory.v1",
        "user.profile-snapshot.v1",
        "management.task-events.v1",
        "management.workspace-events.v1",
        "management.project-progress-snapshot.v1",
        "gamification.progress-events.v1",
        "admin.user-deletion-commands.v1",
        "admin.user-deletion-results.v1",
    ]

    # Consumers (right)
    consumers = [
        ("UserService", 920, 30, "#A5D6A7"),
        ("ManagementService", 920, 110, "#FFB74D"),
        ("GamificationService", 920, 190, "#EF5350"),
        ("AnalyticsService", 920, 270, "#CE93D8"),
        ("MessengerService", 920, 350, "#4FC3F7"),
        ("CallService", 920, 430, "#90A4AE"),
    ]

    # Draw producers
    for name, x, y, color in producers:
        draw.rectangle([x, y, x + 200, y + 50], fill=color, outline="black", width=1)
        bbox = draw.textbbox((0, 0), name, font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (200 - tw) // 2, y + 15), name, fill="black", font=font_bold)

    # Draw Kafka broker
    draw.rectangle([380, 10, 720, H - 10], fill="#FFF9C4", outline="#F57F17", width=2)
    draw.text((490, 15), "Apache Kafka", fill="#F57F17", font=font_bold)
    for i, topic in enumerate(topics):
        ty = 45 + i * 58
        draw.rectangle([395, ty, 705, ty + 42], fill="#FFFDE7", outline="#FBC02D", width=1)
        draw.text((400, ty + 12), topic, fill="#333", font=font)

    # Draw consumers
    for name, x, y, color in consumers:
        draw.rectangle([x, y, x + 200, y + 50], fill=color, outline="black", width=1)
        bbox = draw.textbbox((0, 0), name, font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (200 - tw) // 2, y + 15), name, fill="black", font=font_bold)

    # Arrows: producers -> kafka
    for _, x, y, _ in producers:
        draw.line([(x + 200, y + 25), (380, y + 25)], fill="#333", width=1)
        draw.polygon([(375, y + 20), (380, y + 25), (375, y + 30)], fill="#333")

    # Arrows: kafka -> consumers
    for _, x, y, _ in consumers:
        draw.line([(720, y + 25), (x, y + 25)], fill="#333", width=1)
        draw.polygon([(x - 5, y + 20), (x, y + 25), (x - 5, y + 30)], fill="#333")

    draw.text((390, H - 25), "Producers", fill="#333", font=font)
    draw.text((730, H - 25), "Consumers", fill="#333", font=font)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_gamification_flow_diagram() -> bytes | None:
    """Gamification XP award flow diagram."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1000, 500
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 12)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    steps = [
        ("Пользователь\nзавершает задачу", 30, 50, 180, 60, "#81C784"),
        ("ManagementService\nOutbox: TaskCompleted", 30, 170, 180, 60, "#FFB74D"),
        ("Kafka\ntask-events.v1", 280, 170, 160, 60, "#FFF176"),
        ("GamificationService\nConsumer", 510, 170, 180, 60, "#EF5350"),
        ("Расчёт XP\n+50 XP, +10 coins", 510, 300, 180, 60, "#FFCDD2"),
        ("Обновление\nUserGlobalXp\nUserProjectXp", 280, 300, 180, 70, "#E3F2FD"),
        ("XpTransaction\n(immutable log)", 280, 420, 180, 50, "#F3E5F5"),
        ("GamificationHub\nSignalR: XpAwarded", 760, 170, 180, 60, "#CE93D8"),
        ("Kafka\nprogress-events.v1", 760, 300, 180, 60, "#FFF176"),
        ("AnalyticsService\nОбновление проекций", 760, 420, 180, 60, "#90A4AE"),
    ]

    for label, x, y, w, h, color in steps:
        draw.rectangle([x, y, x + w, y + h], fill=color, outline="black", width=1)
        lines = label.split("\n")
        total_h = len(lines) * 15
        start_y = y + (h - total_h) // 2
        for i, line in enumerate(lines):
            bbox = draw.textbbox((0, 0), line, font=font)
            tw = bbox[2] - bbox[0]
            draw.text((x + (w - tw) // 2, start_y + i * 15), line, fill="black", font=font)

    # Arrows
    arrows = [
        (120, 110, 120, 170), (210, 200, 280, 200), (440, 200, 510, 200),
        (600, 230, 600, 300), (510, 335, 460, 335), (370, 370, 370, 420),
        (690, 200, 760, 200), (850, 230, 850, 300), (850, 360, 850, 420),
    ]
    for x1, y1, x2, y2 in arrows:
        draw.line([(x1, y1), (x2, y2)], fill="#333", width=2)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_ui_sitemap_diagram() -> bytes | None:
    """UI page structure / sitemap diagram."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1100, 550
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 12)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    # Root
    draw.rectangle([450, 10, 650, 50], fill="#1565C0", outline="black", width=2)
    draw.text((490, 20), "Collaby SPA", fill="white", font=font_bold)

    # Level 1: Landing, Platform, Admin
    l1 = [("Landing\n(публичная)", 50, 100, "#81C784"),
          ("Platform\n(авторизованная)", 400, 100, "#FFB74D"),
          ("Admin\n(системный)", 800, 100, "#EF5350")]
    for label, x, y, color in l1:
        draw.rectangle([x, y, x + 200, y + 50], fill=color, outline="black", width=1)
        lines = label.split("\n")
        for i, line in enumerate(lines):
            bbox = draw.textbbox((0, 0), line, font=font)
            tw = bbox[2] - bbox[0]
            draw.text((x + (200 - tw) // 2, y + 5 + i * 18), line, fill="black", font=font)

    draw.line([(550, 50), (150, 100)], fill="#333", width=1)
    draw.line([(550, 50), (500, 100)], fill="#333", width=1)
    draw.line([(550, 50), (900, 100)], fill="#333", width=1)

    # Landing children
    landing_pages = ["Главная /", "Авторизация /auth", "О нас /about", "Контакты /contact",
                     "Политика /privacy", "Условия /terms"]
    for i, page in enumerate(landing_pages):
        y = 190 + i * 35
        draw.rectangle([20, y, 270, y + 28], fill="#C8E6C9", outline="#333", width=1)
        draw.text((30, y + 5), page, fill="black", font=font)

    # Platform children
    platform_pages = ["Dashboard", "Messenger", "Задачи (Kanban)", "Команда",
                      "Аналитика", "Магазин", "Достижения", "AI-ассистент", "Профиль"]
    for i, page in enumerate(platform_pages):
        y = 190 + i * 35
        draw.rectangle([350, y, 600, y + 28], fill="#FFE0B2", outline="#333", width=1)
        draw.text((360, y + 5), page, fill="black", font=font)

    # Admin children
    admin_pages = ["Обзор", "Пользователи", "Рабочие пространства", "Операции", "Лендинг", "Панели"]
    for i, page in enumerate(admin_pages):
        y = 190 + i * 35
        draw.rectangle([750, y, 1000, y + 28], fill="#FFCDD2", outline="#333", width=1)
        draw.text((760, y + 5), page, fill="black", font=font)

    # Connect level 1 to children
    for i in range(len(landing_pages)):
        draw.line([(150, 150), (145, 190 + i * 35)], fill="#999", width=1)
    for i in range(len(platform_pages)):
        draw.line([(500, 150), (475, 190 + i * 35)], fill="#999", width=1)
    for i in range(len(admin_pages)):
        draw.line([(900, 150), (875, 190 + i * 35)], fill="#999", width=1)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _generate_cicd_diagram() -> bytes | None:
    """CI/CD pipeline diagram."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None

    W, H = 1100, 400
    img = Image.new("RGB", (W, H), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
        font_bold = ImageFont.truetype("arialbd.ttf", 13)
    except (OSError, IOError):
        font = ImageFont.load_default()
        font_bold = font

    stages = [
        ("BUILD", 30, "#4FC3F7", [
            "build-identity", "build-management", "build-messenger",
            "build-gamification", "build-agent", "build-frontend",
            "build-postgres", "build-mongodb", "build-redis"
        ]),
        ("TEST", 300, "#81C784", [
            "dotnet-identity", "dotnet-management", "dotnet-gamification",
            "dotnet-analytics", "dotnet-messenger", "dotnet-calls",
            "frontend-tests", "secret-detection"
        ]),
        ("DEPLOY", 600, "#FFB74D", [
            "deploy-prepare", "deploy-launch"
        ]),
    ]

    for name, x, color, jobs in stages:
        # Stage header
        draw.rectangle([x, 20, x + 230, 60], fill=color, outline="black", width=2)
        bbox = draw.textbbox((0, 0), name, font=font_bold)
        tw = bbox[2] - bbox[0]
        draw.text((x + (230 - tw) // 2, 30), name, fill="black", font=font_bold)
        # Jobs
        for i, job in enumerate(jobs):
            jy = 75 + i * 30
            draw.rectangle([x + 10, jy, x + 220, jy + 24], fill="#FAFAFA", outline="#999", width=1)
            draw.text((x + 15, jy + 4), job, fill="#333", font=font)

    # Arrows between stages
    draw.line([(260, 40), (300, 40)], fill="#333", width=2)
    draw.polygon([(295, 35), (300, 40), (295, 45)], fill="#333")
    draw.line([(530, 40), (600, 40)], fill="#333", width=2)
    draw.polygon([(595, 35), (600, 40), (595, 45)], fill="#333")

    draw.text((20, H - 25), "GitHub CI/CD Pipeline (.github/workflows/ci.yml)", fill="#666", font=font_bold)

    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _insert_pillow_diagram(doc: Document, diagram_bytes: bytes | None, caption: str):
    """Insert a Pillow-generated diagram into the document with figure caption."""
    if diagram_bytes is None:
        add_figure_placeholder(doc, caption)
        return

    fig_num = _next_figure()
    buf = BytesIO(diagram_bytes)
    doc.add_picture(buf, width=Cm(13))
    last_p = doc.paragraphs[-1]
    last_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    last_p.paragraph_format.first_line_indent = Cm(0)

    p_cap = doc.add_paragraph()
    p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_cap.paragraph_format.first_line_indent = Cm(0)
    p_cap.paragraph_format.space_before = Pt(3)
    p_cap.paragraph_format.space_after = Pt(12)
    run_cap = p_cap.add_run(f"Рисунок {fig_num} - {caption}")
    run_cap.font.name = FONT_NAME
    run_cap.font.size = Pt(12)


# ---------------------------------------------------------------------------
# Content generators
# ---------------------------------------------------------------------------

def build_title_page_1(doc: Document):
    """First title page."""
    add_empty_line(doc, 2)
    add_centered_text(doc, "МИНИСТЕРСТВО НАУКИ И ВЫСШЕГО ОБРАЗОВАНИЯ", bold=True, size=Pt(12))
    add_centered_text(doc, "РЕСПУБЛИКИ КАЗАХСТАН", bold=True, size=Pt(12))
    add_empty_line(doc)
    add_centered_text(doc, UNIVERSITY, bold=True, size=Pt(13))
    add_empty_line(doc)
    add_centered_text(doc, "Институт информационных и телекоммуникационных технологий", size=Pt(12))
    add_centered_text(doc, DEPARTMENT, size=Pt(12))
    add_centered_text(doc, PROGRAM, size=Pt(12))
    add_empty_line(doc, 4)
    add_centered_text(doc, "ДИПЛОМНЫЙ ПРОЕКТ", bold=True, size=Pt(16))
    add_empty_line(doc)
    add_centered_text(
        doc,
        "на тему: «Разработка веб-платформы для совместной работы Collaby\n"
        "с системой геймификации и AI-ассистентом»",
        bold=True, size=Pt(14)
    )
    add_empty_line(doc, 4)

    # Right-aligned block
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p.paragraph_format.first_line_indent = Cm(0)
    run = p.add_run(
        "Выполнили: студенты группы CS-22\n"
        "ФИО Участника 1\n"
        "ФИО Участника 2\n"
        "ФИО Участника 3"
    )
    run.font.name = FONT_NAME
    run.font.size = Pt(12)

    add_empty_line(doc)
    p2 = doc.add_paragraph()
    p2.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p2.paragraph_format.first_line_indent = Cm(0)
    run2 = p2.add_run("Научный руководитель: _________________")
    run2.font.name = FONT_NAME
    run2.font.size = Pt(12)

    add_empty_line(doc, 4)
    add_centered_text(doc, f"Алматы {YEAR}", size=Pt(14))
    add_page_break(doc)


def build_title_page_2(doc: Document):
    """Title page with defense admission."""
    add_empty_line(doc, 2)
    add_centered_text(doc, UNIVERSITY, bold=True, size=Pt(13))
    add_centered_text(doc, "Институт информационных и телекоммуникационных технологий", size=Pt(12))
    add_centered_text(doc, DEPARTMENT, size=Pt(12))
    add_centered_text(doc, PROGRAM, size=Pt(12))
    add_empty_line(doc, 2)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p.paragraph_format.first_line_indent = Cm(0)
    run = p.add_run(
        "ДОПУЩЕН К ЗАЩИТЕ\n"
        "Заведующий кафедрой\n"
        "_______________ _______________\n"
        f"«____» _______________ {YEAR} г."
    )
    run.font.name = FONT_NAME
    run.font.size = Pt(12)

    add_empty_line(doc, 3)
    add_centered_text(doc, "ДИПЛОМНЫЙ ПРОЕКТ", bold=True, size=Pt(16))
    add_empty_line(doc)
    add_centered_text(
        doc,
        "на тему: «Разработка веб-платформы для совместной работы Collaby\n"
        "с системой геймификации и AI-ассистентом»",
        bold=True, size=Pt(14)
    )
    add_empty_line(doc, 3)
    add_centered_text(doc, PROGRAM, size=Pt(12))
    add_empty_line(doc, 3)

    p2 = doc.add_paragraph()
    p2.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p2.paragraph_format.first_line_indent = Cm(0)
    run2 = p2.add_run(
        "Выполнили: ФИО Участника 1 __________\n"
        " ФИО Участника 2 __________\n"
        " ФИО Участника 3 __________\n\n"
        "Научный руководитель: ________________ __________\n\n"
        "Рецензент: __________________________ __________"
    )
    run2.font.name = FONT_NAME
    run2.font.size = Pt(12)

    add_empty_line(doc, 3)
    add_centered_text(doc, f"Алматы {YEAR}", size=Pt(14))
    add_page_break(doc)


def build_task_assignment(doc: Document):
    """Task assignment (Zadaniye)."""
    add_centered_text(doc, UNIVERSITY, bold=True, size=Pt(11))
    add_centered_text(doc, "Институт информационных и телекоммуникационных технологий", size=Pt(11))
    add_centered_text(doc, DEPARTMENT, size=Pt(11))
    add_centered_text(doc, PROGRAM, size=Pt(11))
    add_empty_line(doc)
    add_centered_text(doc, "ЗАДАНИЕ", bold=True, size=Pt(14))
    add_centered_text(doc, "на выполнение дипломного проекта", bold=True, size=Pt(12))
    add_empty_line(doc)

    add_normal_paragraph(
        doc,
        "Тема: «Разработка веб-платформы для совместной работы Collaby "
        "с системой геймификации и AI-ассистентом»"
    )
    add_empty_line(doc)

    headers = ["№", "Участник", "Роль и зона ответственности"]
    rows = [
        ["1", "ФИО Участника 1",
         "Backend-разработка: IdentityService, ManagementService, "
         "AnalyticsService, инфраструктура (Docker, CI/CD, Ansible)"],
        ["2", "ФИО Участника 2",
         "Backend-разработка: GamificationService, MessengerService, "
         "CallService, интеграция Kafka, система событий"],
        ["3", "ФИО Участника 3",
         "Frontend-разработка: React SPA, UI/UX, "
         "интеграция AgentService (AI-ассистент)"],
    ]
    add_simple_table(doc, headers, rows, "Распределение задач между участниками")

    add_normal_paragraph(
        doc,
        "Цель проекта: спроектировать и разработать веб-платформу Collaby, объединяющую "
        "управление задачами, мессенджер, видеозвонки, систему геймификации и локальный "
        "AI-ассистент на базе модели Qwen 2.5-1.5B в единое решение для совместной работы."
    )
    add_empty_line(doc)
    add_normal_paragraph(doc, "Исходные данные: требования заказчика, обзор существующих платформ (Jira, Slack, MS Teams, Asana, Monday.com, Notion), стандарты ГОСТ и технические спецификации веб-технологий.")
    add_empty_line(doc)
    add_normal_paragraph(
        doc,
        f"Научный руководитель: _________________\n"
        f"Дата выдачи задания: «____» __________ {YEAR} г.\n"
        f"Срок сдачи: «____» __________ {YEAR} г."
    )
    add_page_break(doc)


def build_schedule(doc: Document):
    """Preparation schedule."""
    add_centered_text(doc, "ГРАФИК", bold=True, size=Pt(14))
    add_centered_text(doc, "подготовки дипломного проекта", bold=True, size=Pt(12))
    add_empty_line(doc)

    headers = ["№", "Этап работы", "Сроки", "Примечание"]
    rows = [
        ["1", "Анализ предметной области, обзор аналогов", "Сентябрь 2025", ""],
        ["2", "Обоснование технологий и архитектуры", "Октябрь 2025", ""],
        ["3", "Проектирование базы данных и API", "Ноябрь 2025", ""],
        ["4", "Разработка backend-сервисов (Access, Workspace)", "Декабрь 2025", ""],
        ["5", "Разработка backend-сервисов (Realtime, Engagement)", "Январь 2026", ""],
        ["6", "Разработка frontend SPA", "Февраль 2026", ""],
        ["7", "Интеграция AI-ассистента", "Март 2026", ""],
        ["8", "Тестирование и развёртывание", "Апрель 2026", ""],
        ["9", "Оформление пояснительной записки", "Апрель -Май 2026", ""],
        ["10", "Предзащита и защита дипломного проекта", "Июнь 2026", ""],
    ]
    add_simple_table(doc, headers, rows, "График подготовки дипломного проекта")
    add_page_break(doc)


def build_annotation_ru(doc: Document):
    """Russian annotation."""
    add_centered_text(doc, "АННОТАЦИЯ", bold=True)
    add_empty_line(doc)
    add_normal_paragraph(
        doc,
        "Данный дипломный проект посвящён проектированию и разработке веб-платформы Collaby, "
        "предназначенной для организации совместной работы распределённых команд. Платформа "
        "объединяет управление задачами по методологии Kanban, корпоративный мессенджер с "
        "поддержкой WebRTC-видеозвонков, аналитическую панель и локальный AI-ассистент на базе "
        "языковой модели Qwen 2.5-1.5B."
    )
    add_normal_paragraph(
        doc,
        "Ключевой научной новизной работы является внедрение комплексной системы геймификации "
        "в контекст проектной деятельности: система опыта (XP) с глобальным и проектным уровнем "
        "прогресса, ежедневные и еженедельные квесты, виртуальный магазин с внутренней валютой, "
        "таблицы лидеров, серии активности (streaks) с механикой заморозки, а также деревья навыков "
        "для индивидуальной специализации участников."
    )
    add_normal_paragraph(
        doc,
        "Серверная часть реализована на платформе .NET 10 с сервис-ориентированной архитектурой "
        "(SOA, MSA-ready): консолидированные хосты (access, workspace, realtime, engagement) "
        "содержат независимые доменные модули, каждый из которых может быть извлечён в отдельный "
        "микросервис при необходимости масштабирования. Межсервисное взаимодействие обеспечивается "
        "Apache Kafka. Клиентская часть выполнена как SPA на React 19 с TailwindCSS 4. "
        "Инфраструктура контейнеризирована в Docker и развёрнута с помощью Ansible."
    )
    add_normal_paragraph(
        doc,
        "Результатом работы является полнофункциональный прототип платформы, прошедший развёртывание "
        "на продуктивном сервере с использованием Cloudflare Zero Trust."
    )
    add_page_break(doc)


def build_annotation_kz(doc: Document):
    """Kazakh annotation."""
    add_centered_text(doc, "АҢДАТПА", bold=True)
    add_empty_line(doc)
    add_normal_paragraph(
        doc,
        "Бұл дипломдық жоба таратылған командалардың бірлескен жұмысын ұйымдастыруға арналған "
        "Collaby веб-платформасын жобалау мен әзірлеуге арналған. Платформа Kanban әдіснамасы "
        "бойынша тапсырмаларды басқаруды, WebRTC бейне қоңырауларын қолдайтын корпоративтік "
        "мессенджерді, аналитикалық панельді және Qwen 2.5-1.5B тілдік моделіне негізделген "
        "жергілікті AI-көмекшіні біріктіреді."
    )
    add_normal_paragraph(
        doc,
        "Жұмыстың ғылыми жаңалығы жобалық қызмет контекстіне кешенді геймификация жүйесін "
        "енгізу болып табылады: жаһандық және жобалық деңгейдегі тәжірибе жүйесі (XP), "
        "күнделікті және апталық квесттер, ішкі валютасы бар виртуалды дүкен, көшбасшылар "
        "кестелері, белсенділік сериялары (streaks) және дағдылар ағашы."
    )
    add_normal_paragraph(
        doc,
        "Серверлік бөлім .NET 10 платформасында сервис-бағдарлы архитектурамен (SOA, MSA-ready) "
        "іске асырылды. Клиенттік бөлім React 19 және TailwindCSS 4 негізіндегі SPA ретінде "
        "орындалды. Инфрақұрылым Docker-да контейнерлендірілді және Ansible арқылы орналастырылды."
    )
    add_page_break(doc)


def build_annotation_en(doc: Document):
    """English annotation."""
    add_centered_text(doc, "ANNOTATION", bold=True)
    add_empty_line(doc)
    add_normal_paragraph(
        doc,
        "This diploma project is dedicated to the design and development of the Collaby web platform "
        "for organizing collaborative work of distributed teams. The platform integrates Kanban-based "
        "task management, a corporate messenger with WebRTC video call support, an analytics dashboard, "
        "and a local AI assistant based on the Qwen 2.5-1.5B language model."
    )
    add_normal_paragraph(
        doc,
        "The key scientific novelty of the work is the implementation of a comprehensive gamification "
        "system in the context of project activities: an experience point (XP) system with global and "
        "per-project progress levels, daily and weekly quests, a virtual shop with an internal currency, "
        "leaderboards, activity streaks with a freeze mechanic, and skill trees for individual "
        "specialization of team members."
    )
    add_normal_paragraph(
        doc,
        "The server-side is implemented on the .NET 10 platform using a service-oriented architecture "
        "(SOA, MSA-ready): consolidated hosts (access, workspace, realtime, engagement) contain "
        "independent domain modules, each of which can be extracted into a separate microservice "
        "when scaling is required. Inter-service communication is provided by Apache Kafka. "
        "The client-side is built as a SPA using React 19 with TailwindCSS 4. "
        "The infrastructure is containerized with Docker and deployed using Ansible."
    )
    add_page_break(doc)


def build_introduction(doc: Document):
    """Введение."""
    global _current_section
    _current_section = 0

    # Add a new section so page numbering starts here
    from docx.enum.section import WD_ORIENT
    new_section = doc.add_section()
    new_section.page_width = Cm(21.0)
    new_section.page_height = Cm(29.7)
    new_section.left_margin = LEFT_MARGIN
    new_section.right_margin = RIGHT_MARGIN
    new_section.top_margin = TOP_MARGIN
    new_section.bottom_margin = BOTTOM_MARGIN
    # Restart page numbering from 1 is not needed - GOST counts from title page
    # but numbers only shown from Introduction onwards

    h = doc.add_paragraph("ВВЕДЕНИЕ", style="Heading 1")
    h.paragraph_format.page_break_before = False  # section break already creates new page

    add_normal_paragraph(
        doc,
        "В условиях глобального роста дистанционной работы и распределённых команд организация "
        "эффективного взаимодействия между участниками проектов становится критически важной задачей. "
        "По данным аналитических исследований, к 2025 году более 70% компаний в сфере информационных "
        "технологий используют гибридный или полностью удалённый формат работы. При этом типичная "
        "команда разработчиков вынуждена одновременно использовать от трёх до пяти различных "
        "инструментов: систему управления задачами (Jira, Asana), корпоративный мессенджер (Slack, "
        "MS Teams), платформу видеоконференций (Zoom, Google Meet), систему аналитики и документацию. "
        "Фрагментация инструментария приводит к потере контекста, снижению продуктивности и "
        "увеличению когнитивной нагрузки на участников."
    )

    add_normal_paragraph(
        doc,
        "Проблема фрагментации рабочих инструментов усугубляется необходимостью постоянного переключения "
        "между различными приложениями, каждое из которых обладает собственным интерфейсом, системой "
        "уведомлений и моделью данных. Исследования в области когнитивной психологии демонстрируют, что "
        "каждое переключение контекста между приложениями занимает от 15 до 25 минут для полного "
        "восстановления концентрации. При пяти-семи переключениях в час, характерных для типичного "
        "рабочего дня распределённой команды, потери продуктивности могут достигать 40% рабочего времени. "
        "Данная проблема особенно критична для команд, работающих по методологиям Agile и Kanban, где "
        "быстрая обратная связь и непрерывная коммуникация являются ключевыми факторами успеха. "
        "Разрыв между инструментом управления задачами и средством коммуникации создаёт информационные "
        "силосы, затрудняющие принятие оперативных решений."
    )

    add_normal_paragraph(
        doc,
        "Отдельной проблемой является мотивация участников проектной деятельности. Существующие "
        "платформы для управления проектами практически не используют механики геймификации, "
        "несмотря на научно подтверждённую эффективность игровых элементов для повышения "
        "вовлечённости и продуктивности. Методы геймификации, успешно применяемые в образовательных "
        "платформах (Duolingo, Khan Academy) и социальных сетях, остаются практически не "
        "интегрированными в инструменты разработки программного обеспечения. Между тем, теория "
        "самодетерминации (Self-Determination Theory, SDT), предложенная Деси и Райаном, убедительно "
        "показывает, что удовлетворение трёх базовых психологических потребностей - автономии, "
        "компетентности и связанности - является ключевым фактором внутренней мотивации. "
        "Геймификация, реализованная через систему прогрессии, достижений и социального сравнения, "
        "непосредственно адресует указанные потребности."
    )

    add_normal_paragraph(
        doc,
        "Актуальность работы обусловлена необходимостью создания единой интегрированной "
        "платформы, которая объединяет все ключевые функции совместной работы и дополняет "
        "их системой геймификации для повышения мотивации и вовлечённости участников. "
        "Дополнительным фактором актуальности является стремительное развитие технологий "
        "искусственного интеллекта и появление компактных языковых моделей, способных работать "
        "локально без обращения к облачным сервисам. Интеграция AI-ассистента непосредственно "
        "в платформу для совместной работы позволяет автоматизировать рутинные операции, такие "
        "как анализ проектного контекста, генерация черновиков задач на основе записей встреч "
        "и формирование рекомендаций по распределению работы. При этом локальное развёртывание "
        "модели обеспечивает конфиденциальность проектных данных, что является критическим "
        "требованием для корпоративных пользователей."
    )

    add_normal_paragraph(doc, "Целью дипломного проекта является проектирование и разработка веб-платформы Collaby, объединяющей управление задачами, мессенджер, видеозвонки, систему геймификации и AI-ассистент в единое решение для совместной работы распределённых команд.", bold=True)

    add_normal_paragraph(doc, "Для достижения поставленной цели необходимо решить следующие задачи:")

    tasks = [
        "провести анализ предметной области и обзор существующих решений для совместной работы;",
        "обосновать выбор технологий и архитектурных решений;",
        "спроектировать сервис-ориентированную архитектуру с поддержкой извлечения модулей в микросервисы;",
        "разработать систему геймификации с механиками XP, квестов, магазина, лидербордов, серий и деревьев навыков;",
        "реализовать интеграцию локального AI-ассистента на базе модели Qwen 2.5-1.5B;",
        "разработать клиентское SPA-приложение на React 19 с адаптивным дизайном;",
        "выполнить развёртывание системы в контейнерной среде Docker с автоматизацией через Ansible и CI/CD."
    ]
    for i, task in enumerate(tasks, 1):
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = FIRST_LINE_INDENT
        run = p.add_run(f"{i}) {task}")
        run.font.name = FONT_NAME
        run.font.size = FONT_SIZE

    add_normal_paragraph(
        doc,
        "Объектом исследования является процесс организации совместной работы распределённых "
        "команд разработчиков программного обеспечения."
    )
    add_normal_paragraph(
        doc,
        "Предметом исследования являются методы и средства интеграции управления проектами, "
        "коммуникации и геймификации в единой веб-платформе."
    )
    add_normal_paragraph(
        doc,
        "Научная новизна работы заключается во внедрении комплексной системы геймификации "
        "в контекст проектной деятельности. В отличие от существующих платформ, которые "
        "ограничиваются простыми бейджами или очками, Collaby реализует полноценную игровую "
        "экономику: двухуровневую систему опыта (глобальный и проектный XP), ежедневные "
        "и еженедельные квесты с оконной системой назначения, магазин виртуальных предметов "
        "с внутренней валютой, серии активности с механикой заморозки и деревья навыков "
        "для индивидуальной специализации. Дополнительным элементом новизны является "
        "интеграция локального AI-ассистента для анализа проектного контекста. Научная "
        "новизна также проявляется в архитектурном решении SOA (MSA-ready), позволяющем "
        "сочетать простоту разработки и отладки, характерные для монолитных приложений, "
        "с модульностью и возможностью масштабирования, свойственными микросервисной архитектуре."
    )
    add_normal_paragraph(
        doc,
        "Практическая значимость работы состоит в создании полнофункционального прототипа "
        "платформы, развёрнутого на продуктивном сервере и доступного через защищённый "
        "канал Cloudflare Zero Trust. Платформа может использоваться как основа для "
        "коммерческого продукта или образовательного инструмента. Разработанная система "
        "геймификации является универсальной и может быть адаптирована для применения в "
        "любых проектно-ориентированных рабочих процессах, включая образовательные учреждения, "
        "стартапы и корпоративные команды. Инфраструктурные решения, реализованные в проекте "
        "(контейнеризация, CI/CD, автоматическое развёртывание через Ansible), представляют "
        "собой шаблон для развёртывания аналогичных сервис-ориентированных систем."
    )

    add_normal_paragraph(
        doc,
        "Методологической основой работы являются принципы сервис-ориентированного проектирования, "
        "теория геймификации (Octalysis Framework Ю Кая Чоу, типология игроков Бартла, теория "
        "самодетерминации Деси и Райана), методологии гибкой разработки (Agile, Kanban), а также "
        "паттерны проектирования распределённых систем (Transactional Outbox, CQRS, Event-driven "
        "Architecture, Saga) [9, 10, 11]. Работа опирается на стандарты ГОСТ 34.602-2020 [22] (техническое задание) "
        "и ГОСТ 7.32-2017 [23] (оформление отчёта)."
    )


# ---------------------------------------------------------------------------
# Section 1
# ---------------------------------------------------------------------------

def build_section_1(doc: Document):
    global _current_section
    _current_section = 1

    doc.add_paragraph("1 ИССЛЕДОВАТЕЛЬСКО-ТЕХНОЛОГИЧЕСКИЙ РАЗДЕЛ", style="Heading 1")

    # 1.1
    doc.add_paragraph("1.1 Анализ предметной области", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Современная разработка программного обеспечения характеризуется высокой степенью "
        "распределённости команд и необходимостью непрерывной коммуникации. Методологии Agile "
        "и Kanban, ставшие индустриальным стандартом, требуют наличия инструментов, обеспечивающих "
        "прозрачность процесса, визуализацию потока задач и мгновенную обратную связь между "
        "участниками. Kanban-доски, изначально разработанные в рамках производственной системы "
        "Toyota, нашли широкое применение в IT-индустрии благодаря наглядности и простоте "
        "управления задачами через статусные колонки."
    )
    add_normal_paragraph(
        doc,
        "Методология Kanban основывается на четырёх ключевых принципах: визуализация рабочего "
        "процесса, ограничение незавершённой работы (WIP-лимиты), управление потоком и непрерывное "
        "улучшение. В контексте разработки программного обеспечения Kanban-доска представляет собой "
        "визуальное отображение конвейера задач, где каждая колонка соответствует определённому "
        "этапу жизненного цикла задачи: Backlog, To Do, In Progress, Review, Done. Перемещение "
        "карточек между колонками отражает продвижение задачи по конвейеру и позволяет всем "
        "участникам команды мгновенно оценивать текущее состояние проекта. В платформе Collaby "
        "реализована полноценная Kanban-доска с настраиваемыми статусными колонками (сущность "
        "ProjectStatus), поддержкой drag-and-drop через библиотеку dnd-kit, метками (ProjectLabel), "
        "приоритетами задач и обновлением в реальном времени через SignalR."
    )
    add_normal_paragraph(
        doc,
        "Платформы для совместной работы (collaboration platforms) представляют собой класс "
        "программного обеспечения, объединяющего функции управления задачами, обмена сообщениями, "
        "файлами и видеоконференцсвязи. Рынок таких платформ демонстрирует устойчивый рост: "
        "к 2025 году объём мирового рынка инструментов для совместной работы превысил 20 "
        "миллиардов долларов. Однако большинство решений фокусируются на отдельных аспектах "
        "работы, вынуждая команды использовать несколько продуктов одновременно. Ведущие "
        "аналитические компании, такие как Gartner и Forrester, выделяют категорию Integrated "
        "Workplace Platforms (IWP), определяя её как комплексные решения, объединяющие "
        "коммуникацию, управление проектами, документооборот и аналитику в едином интерфейсе. "
        "Тем не менее, даже среди IWP-платформ отсутствуют решения, интегрирующие механики "
        "геймификации на уровне ядра системы."
    )
    add_normal_paragraph(
        doc,
        "Геймификация (gamification) - применение игровых механик в неигровом контексте [12] - "
        "представляет собой перспективный подход к повышению мотивации и вовлечённости участников "
        "проектной деятельности [14]. Исследования показывают [13], что внедрение элементов геймификации "
        "(очки опыта, уровни, достижения, лидерборды) способно увеличить продуктивность на "
        "20-40%. При этом в индустрии разработки программного обеспечения геймификация "
        "практически не применяется: существующие платформы управления проектами ограничиваются "
        "примитивными бейджами или полностью лишены игровых элементов."
    )
    add_normal_paragraph(
        doc,
        "Теоретической основой геймификации в контексте данной работы выступают три ключевые "
        "модели. Первая - типология игроков Ричарда Бартла (Bartle Player Types) [15], которая "
        "классифицирует участников по четырём архетипам: Achievers (достигатели, мотивированные "
        "прогрессом и накоплением ресурсов), Explorers (исследователи, стремящиеся к открытию "
        "нового функционала), Socializers (социализаторы, ориентированные на взаимодействие "
        "с другими участниками) и Killers (конкуренты, мотивированные соревнованием). Система "
        "геймификации Collaby адресует все четыре архетипа: Achievers удовлетворяются системой "
        "XP и уровней, Explorers - деревьями навыков с ветвлением, Socializers - "
        "командными квестами и мессенджером, Killers - лидербордами и соревновательным "
        "рейтингом."
    )
    add_normal_paragraph(
        doc,
        "Вторая модель - Octalysis Framework Ю Кай Чоу [16], определяющая восемь основных "
        "драйверов мотивации: Epic Meaning (эпическое значение), Development & Accomplishment "
        "(развитие и достижение), Empowerment of Creativity (расширение возможностей творчества), "
        "Ownership & Possession (владение и обладание), Social Influence (социальное влияние), "
        "Scarcity & Impatience (дефицит и нетерпение), Unpredictability & Curiosity "
        "(непредсказуемость и любопытство), Loss & Avoidance (потеря и избегание). В Collaby "
        "драйвер Development реализован через систему XP и уровней, Ownership - через "
        "инвентарь и экипировку предметов магазина, Social Influence - через лидерборды, "
        "Scarcity - через лимитированные предметы магазина, Loss & Avoidance - через "
        "механику серий активности (streaks) с угрозой потери прогресса."
    )
    add_normal_paragraph(
        doc,
        "Третья модель - теория самодетерминации (Self-Determination Theory, SDT) Деси "
        "и Райана [17], которая выделяет три базовые психологические потребности: автономия "
        "(потребность в самостоятельном выборе действий), компетентность (потребность в ощущении "
        "мастерства и прогресса) и связанность (потребность в социальном взаимодействии). "
        "Геймификация Collaby удовлетворяет автономию через выбор направления в деревьях навыков "
        "и настройку аватара, компетентность - через систему уровней и видимый прогресс, "
        "связанность - через командные лидерборды и совместные квесты. Данный теоретический "
        "фундамент обеспечивает научную обоснованность проектных решений в области геймификации "
        "и позволяет прогнозировать положительное влияние на вовлечённость участников."
    )

    # 1.2
    doc.add_paragraph("1.2 Обзор существующих решений", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Для обоснования необходимости разработки платформы Collaby проведён сравнительный "
        "анализ наиболее популярных решений для совместной работы и управления проектами. "
        "В качестве критериев сравнения выбраны ключевые функциональные возможности, "
        "отражающие потребности современных распределённых команд."
    )

    add_normal_paragraph(
        doc,
        "Jira (Atlassian) является наиболее распространённой системой управления проектами в "
        "IT-индустрии. Платформа предоставляет мощные средства трекинга задач, поддерживает "
        "методологии Scrum и Kanban, обладает развитой системой фильтрации и поиска через JQL "
        "(Jira Query Language). К преимуществам Jira относятся глубокая настраиваемость рабочих "
        "процессов, обширная экосистема плагинов (Atlassian Marketplace) и интеграция с другими "
        "продуктами Atlassian (Confluence, Bitbucket). Однако Jira обладает существенными "
        "недостатками: отсутствие встроенного мессенджера и видеозвонков (для коммуникации "
        "требуется отдельная подписка на Slack или MS Teams), сложный и перегруженный интерфейс, "
        "высокая стоимость для крупных команд (от $7.75 за пользователя в месяц на тарифе "
        "Standard), полное отсутствие механик геймификации в базовой поставке. Попытки добавить "
        "геймификацию через сторонние плагины (Gamification for Jira) ограничиваются простыми "
        "бейджами и не предоставляют комплексной игровой экономики."
    )
    add_normal_paragraph(
        doc,
        "Asana позиционируется как универсальная платформа для управления работой с акцентом на "
        "простоту использования. Платформа поддерживает различные представления задач (список, "
        "Kanban-доска, таймлайн, календарь), предоставляет средства автоматизации через правила "
        "и шаблоны проектов. К достоинствам Asana относятся интуитивный интерфейс, гибкая система "
        "зависимостей между задачами и портфели проектов для верхнеуровневого управления. Недостатки "
        "включают отсутствие встроенного мессенджера (только комментарии к задачам), отсутствие "
        "видеозвонков, закрытый исходный код и отсутствие возможности self-hosting. AI-функции "
        "Asana Intelligence ограничены генерацией описаний задач и не предоставляют анализ "
        "проектного контекста. Геймификация в Asana полностью отсутствует."
    )
    add_normal_paragraph(
        doc,
        "Monday.com представляет собой платформу для управления рабочими процессами с визуальным "
        "интерфейсом и широкими возможностями кастомизации. Платформа поддерживает различные "
        "типы колонок (статус, дата, числа, формулы), интеграции с более чем 200 внешними "
        "сервисами и автоматизации на основе триггеров. Преимуществами Monday являются визуальная "
        "привлекательность интерфейса, гибкость структуры данных и мощные средства отчётности. "
        "К недостаткам относятся высокая стоимость (от $9 за пользователя в месяц, минимум 3 "
        "пользователя), отсутствие встроенного мессенджера и видеозвонков, ограниченная "
        "поддержка Kanban и полное отсутствие геймификации."
    )
    add_normal_paragraph(
        doc,
        "Slack является ведущим корпоративным мессенджером с поддержкой каналов, прямых сообщений, "
        "интеграций (Slack Apps) и аудио/видеозвонков (Huddles). К преимуществам Slack относятся "
        "удобный интерфейс обмена сообщениями, обширная экосистема приложений, поддержка потоков "
        "(threads) для организации дискуссий и Slack AI для суммаризации каналов. Недостатки "
        "Slack включают отсутствие полноценного управления задачами (только базовые списки через "
        "Slack Lists), слабую поддержку Kanban-досок, отсутствие self-hosting и высокую стоимость "
        "для больших команд. Геймификация в Slack отсутствует."
    )
    add_normal_paragraph(
        doc,
        "Microsoft Teams является частью экосистемы Microsoft 365 и представляет собой платформу "
        "для коммуникации и совместной работы с интеграцией в продукты Microsoft (SharePoint, "
        "OneDrive, Planner). Teams предоставляет мессенджер с поддержкой каналов, видеоконференции "
        "с записью и транскрипцией, а также базовое управление задачами через Planner. Преимуществами "
        "Teams являются глубокая интеграция с Microsoft 365, корпоративный уровень безопасности "
        "и Copilot AI для генерации резюме встреч. Недостатки включают тяжеловесный интерфейс, "
        "зависимость от экосистемы Microsoft, ограниченные возможности Kanban (Planner не поддерживает "
        "настраиваемые колонки), закрытый исходный код и полное отсутствие геймификации."
    )
    add_normal_paragraph(
        doc,
        "Notion позиционируется как универсальное рабочее пространство (all-in-one workspace) "
        "с поддержкой документов, баз данных, Kanban-досок и вики. К преимуществам Notion относятся "
        "гибкая блочная структура контента, поддержка связанных баз данных, шаблоны страниц "
        "и Notion AI для генерации и редактирования текста. Недостатки включают отсутствие "
        "встроенного мессенджера и видеозвонков, ограниченную производительность при работе "
        "с большими базами данных, отсутствие self-hosting и полное отсутствие геймификации. "
        "Kanban в Notion реализован через представления баз данных, но не поддерживает "
        "специализированные функции управления проектами (приоритеты, подзадачи, зависимости)."
    )

    headers = [
        "Критерий", "Jira", "Asana", "Monday", "Slack", "MS Teams", "Notion", "Collaby"
    ]
    rows = [
        ["Управление задачами", "+", "+", "+", " -", "+-", "+", "+"],
        ["Kanban-доска", "+", "+", "+", " -", " -", "+-", "+"],
        ["Мессенджер", " -", " -", " -", "+", "+", " -", "+"],
        ["Видеозвонки", " -", " -", " -", "+", "+", " -", "+"],
        ["Геймификация", " -", " -", " -", " -", " -", " -", "+"],
        ["AI-ассистент", "+-", "+-", "+-", "+", "+", "+", "+"],
        ["Self-hosting", "+-", " -", " -", " -", " -", " -", "+"],
        ["Open-source", " -", " -", " -", " -", " -", " -", "+"],
    ]
    add_simple_table(doc, headers, rows, "Сравнительный анализ существующих решений")

    add_normal_paragraph(
        doc,
        "Как видно из таблицы, ни одна из существующих платформ не объединяет все рассматриваемые "
        "функции. Jira и Asana фокусируются на управлении задачами, но не предоставляют "
        "встроенного мессенджера и видеозвонков. Slack и MS Teams решают задачу коммуникации, "
        "но слабо поддерживают управление проектами. Ни одна из платформ не реализует комплексную "
        "систему геймификации. Notion предлагает гибкую структуру данных, но лишён специализированных "
        "функций для Kanban-управления и реального времени. Данный анализ подтверждает целесообразность "
        "разработки интегрированной платформы Collaby."
    )

    add_normal_paragraph(
        doc,
        "Отдельного внимания заслуживает функция геймификации. Существующие попытки внедрения "
        "геймификации в рабочие инструменты ограничиваются простыми бейджами за достижения "
        "(например, в Jira через сторонние плагины). Ни одна из платформ не предлагает "
        "комплексной игровой экономики с системой опыта, квестами, магазином, сериями "
        "активности и деревьями навыков. Платформа Collaby восполняет данный пробел, "
        "предоставляя полноценную систему геймификации, научно обоснованную на базе теории "
        "самодетерминации и фреймворка Octalysis."
    )

    # 1.3
    doc.add_paragraph("1.3 Обоснование выбора технологий", style="Heading 2")

    add_normal_paragraph(doc, "Серверная часть (Backend):", bold=True)
    add_normal_paragraph(
        doc,
        "В качестве основной платформы для серверной разработки выбран .NET 10 с ASP.NET Core [1, 2]. "
        "Выбор обусловлен несколькими ключевыми факторами. Во-первых, платформа .NET обеспечивает "
        "высокую производительность: согласно тестам TechEmpower (Round 22), ASP.NET Core входит "
        "в тройку лидеров по показателю Plaintext и JSON Serialization среди всех веб-фреймворков, "
        "превосходя Spring Boot (Java), Express.js (Node.js) и Django (Python). Во-вторых, язык C# "
        "предоставляет строгую типизацию, nullable reference types, паттерн matching и развитую "
        "поддержку асинхронного программирования через async/await, что критически важно для "
        "обработки большого числа параллельных соединений в мессенджере и видеозвонках."
    )
    add_normal_paragraph(
        doc,
        "Рассматривались следующие альтернативы для серверной платформы. Java 21 с Spring Boot 3 "
        "является зрелой платформой с обширной экосистемой, однако характеризуется более высоким "
        "потреблением памяти и более медленным временем старта по сравнению с .NET 10 (Spring Boot "
        "приложение с типичным набором зависимостей запускается за 4-8 секунд, тогда как "
        "ASP.NET Core - за 1-2 секунды). Node.js с Express или Fastify обеспечивает "
        "единый язык (JavaScript/TypeScript) для фронтенда и бэкенда, но однопоточная модель "
        "выполнения и отсутствие строгой типизации на уровне рантайма делают Node.js менее "
        "подходящим для сложных доменных моделей с множеством сущностей. Go (Golang) демонстрирует "
        "отличную производительность и простоту конкурентного программирования, но обладает менее "
        "развитой ORM-экосистемой и ограниченными средствами обобщённого программирования по "
        "сравнению с C#."
    )

    headers_backend = ["Критерий", ".NET 10 (C#)", "Java 21 (Spring)", "Node.js (TS)", "Go"]
    rows_backend = [
        ["Производительность", "Высокая", "Средне-высокая", "Средняя", "Высокая"],
        ["Типизация", "Строгая, Nullable", "Строгая", "Опциональная (TS)", "Строгая"],
        ["ORM", "EF Core 10", "Hibernate", "Prisma/TypeORM", "GORM"],
        ["Async/Await", "Нативная", "Virtual Threads", "Event Loop", "Goroutines"],
        ["Время старта", "1-2 сек", "4-8 сек", "1-3 сек", "<1 сек"],
        ["Потребление RAM", "~80 МБ", "~200 МБ", "~60 МБ", "~30 МБ"],
        ["Экосистема для web", "Развитая", "Развитая", "Развитая", "Ограниченная"],
        ["SignalR поддержка", "Нативная", "Нет (WebSocket)", "Socket.IO", "Нет"],
    ]
    add_simple_table(doc, headers_backend, rows_backend, "Сравнение серверных платформ")

    add_normal_paragraph(
        doc,
        "Entity Framework Core 10.0.5 [3] используется в качестве ORM для работы с PostgreSQL, "
        "обеспечивая Code-First миграции, отслеживание изменений (Change Tracking), ленивую "
        "и жадную загрузку связанных данных, LINQ-запросы с трансляцией в SQL и пакетные "
        "операции (Bulk Insert/Update). Каждый доменный модуль определяет собственный DbContext "
        "с изолированной строкой подключения, что обеспечивает чёткие границы ответственности "
        "на уровне базы данных."
    )
    add_normal_paragraph(
        doc,
        "Дополнительные серверные библиотеки: FluentValidation 11.3.1 обеспечивает декларативную "
        "валидацию входных моделей с поддержкой цепочек правил и локализации сообщений об ошибках; "
        "BCrypt.Net 4.1.0 реализует адаптивное хеширование паролей с настраиваемым cost factor; "
        "MailKit 4.15.1 предоставляет SMTP-клиент для отправки электронной почты с поддержкой "
        "DKIM-подписи и MIME-вложений; Otp.NET 1.4.1 реализует генерацию и верификацию TOTP-кодов "
        "для двухфакторной аутентификации; Confluent.Kafka 2.13.2 предоставляет высокопроизводительный "
        "клиент Apache Kafka для .NET с поддержкой сериализации через JSON и Protobuf."
    )

    add_normal_paragraph(doc, "Клиентская часть (Frontend):", bold=True)
    add_normal_paragraph(
        doc,
        "Клиентская часть реализована как одностраничное приложение (SPA) на React 19.2.4 [4] "
        "с использованием сборщика Vite 8.0.1 [20]. React выбран благодаря компонентному подходу, "
        "развитой экосистеме и поддержке Server Components. Рассматривались альтернативы: "
        "Vue 3 с Composition API предоставляет более простую модель реактивности и лучшую "
        "документацию для начинающих, однако экосистема React значительно шире, а сообщество "
        " - активнее; Angular 18 обладает мощным DI-контейнером и строгой структурой проекта, "
        "но характеризуется более высоким порогом входа, большим размером бандла и менее гибкой "
        "компонентной моделью; Svelte 5 демонстрирует отличную производительность за счёт "
        "компиляции в чистый JavaScript, но обладает менее развитой экосистемой библиотек."
    )

    headers_frontend = ["Критерий", "React 19", "Vue 3", "Angular 18", "Svelte 5"]
    rows_frontend = [
        ["Размер бандла (мин.)", "~42 КБ", "~33 КБ", "~130 КБ", "~2 КБ"],
        ["Экосистема библиотек", "Обширная", "Развитая", "Развитая", "Ограниченная"],
        ["TypeScript поддержка", "Полная", "Полная", "Нативная", "Полная"],
        ["SSR/SSG", "Next.js / Remix", "Nuxt", "Angular Universal", "SvelteKit"],
        ["Компонентная модель", "JSX + Hooks", "SFC + Composition", "Декораторы + DI", "SFC + Runes"],
        ["Сообщество (npm/week)", "~25M", "~5M", "~3M", "~1M"],
        ["Анимации", "Framer Motion", "Transition", "Angular Animations", "Svelte Transition"],
    ]
    add_simple_table(doc, headers_frontend, rows_frontend, "Сравнение фронтенд-фреймворков")

    add_normal_paragraph(
        doc,
        "Vite 8.0.1 выбран в качестве сборщика благодаря мгновенному запуску сервера разработки "
        "(Hot Module Replacement на основе ESM), высокой скорости сборки через Rollup и поддержке "
        "модульной архитектуры. В качестве альтернатив рассматривались Webpack 5 (более медленный "
        "запуск, сложная конфигурация) и Turbopack (экспериментальный статус, ограниченная экосистема "
        "плагинов). TailwindCSS 4.2.2 применяется для стилизации на основе утилитарных классов, "
        "что обеспечивает консистентность дизайна, минимизацию CSS-кода через tree-shaking и "
        "возможность быстрой итерации дизайна без написания кастомных стилей [21]."
    )
    add_normal_paragraph(
        doc,
        "Framer Motion 12.38.0 обеспечивает декларативные анимации интерфейса с поддержкой "
        "spring physics, gesture animations и layout animations; Chart.js 4.5.1 используется "
        "для визуализации аналитических данных (графики активности, прогресс по проектам, "
        "распределение задач); dnd-kit реализует функциональность drag-and-drop для Kanban-доски "
        "с поддержкой сортировки и переноса между колонками; i18next 25.10.2 обеспечивает "
        "интернационализацию с тремя языками (RU/EN/KZ) и динамической загрузкой переводов; "
        "SignalR 10.0.0 [28] (@microsoft/signalr) обеспечивает двунаправленную связь в реальном "
        "времени для обновления Kanban-доски, мессенджера, уведомлений геймификации и WebRTC-сигнализации [18]."
    )

    add_normal_paragraph(doc, "Хранение данных:", bold=True)
    add_normal_paragraph(
        doc,
        "Система использует подход polyglot persistence с тремя типами хранилищ, что позволяет "
        "использовать оптимальное хранилище для каждого типа данных. PostgreSQL [5] выбран в качестве "
        "основной реляционной СУБД благодаря поддержке ACID-транзакций, JSON-операторов, "
        "полнотекстового поиска, оконных функций и расширяемости через extensions. В качестве "
        "альтернативы рассматривалась MySQL 8, однако PostgreSQL демонстрирует лучшую поддержку "
        "сложных запросов, JSON-типов данных (jsonb) и конкурентного доступа через MVCC."
    )

    headers_db = ["Критерий", "PostgreSQL", "MySQL 8", "SQL Server", "SQLite"]
    rows_db = [
        ["ACID-транзакции", "+", "+", "+", "+"],
        ["JSON-поддержка (jsonb)", "+", "Ограниченная", "+", "Нет"],
        ["Полнотекстовый поиск", "+", "+", "+", "FTS5"],
        ["Расширения", "+", "Нет", "Нет", "Нет"],
        ["MVCC", "+", "+", "+", "WAL"],
        ["Стоимость", "Бесплатная", "Бесплатная", "Платная", "Бесплатная"],
        ["Совместимость с EF Core", "Полная (Npgsql)", "Полная (Pomelo)", "Полная", "Полная"],
    ]
    add_simple_table(doc, headers_db, rows_db, "Сравнение реляционных СУБД")

    add_normal_paragraph(
        doc,
        "Каждый доменный модуль работает с изолированной базой данных PostgreSQL (collaby_identity, "
        "collaby_users, collaby_management, collaby_analytics, collaby_gamification), что "
        "обеспечивает границы ответственности и возможность независимого масштабирования. "
        "Подключение осуществляется через Npgsql 10.0.1 с пулированием соединений."
    )
    add_normal_paragraph(
        doc,
        "MongoDB [6] (MongoDB.Driver 3.7.1) используется для документо-ориентированных данных, "
        "где гибкая схема и высокая скорость записи являются приоритетными: чаты и сообщения "
        "мессенджера (collaby_messenger), сессии звонков (collaby_calls), журнал игровых событий "
        "(collaby_gamification). Выбор MongoDB обусловлен естественной моделью данных для чатов "
        "(вложенные документы, массивы участников), горизонтальной масштабируемостью через шардинг "
        "и высокой скоростью операций вставки."
    )
    add_normal_paragraph(
        doc,
        "Redis [7] (StackExchange.Redis 2.12.4) выступает в роли кеша, хранилища временных данных "
        "(rate limiting через скользящее окно), а также поддерживает sorted sets для таблиц "
        "лидеров и pub/sub для координации между инстансами. Для каждого сервиса настроены "
        "отдельные учётные записи Redis ACL с минимальными привилегиями, что реализует принцип "
        "наименьших привилегий (Principle of Least Privilege)."
    )

    add_normal_paragraph(doc, "Событийная шина:", bold=True)
    add_normal_paragraph(
        doc,
        "Apache Kafka [8] (Confluent.Kafka 2.13.2) обеспечивает асинхронное межсервисное "
        "взаимодействие через 9 типизированных топиков. Kafka выбран благодаря гарантиям "
        "доставки (at-least-once семантика), возможности повторной обработки событий через "
        "consumer offset management, горизонтальному масштабированию через партиционирование "
        "и высокой пропускной способности (сотни тысяч сообщений в секунду). В качестве "
        "альтернативы рассматривался RabbitMQ, однако Kafka обеспечивает лучшую поддержку "
        "event sourcing, хранение журнала событий с настраиваемым retention и возможность "
        "replay для восстановления состояния."
    )
    add_normal_paragraph(
        doc,
        "Все топики определены в централизованном реестре (topic-registry.json) и "
        "автоматически создаются при запуске кластера через bootstrap-скрипт "
        "(events-bootstrap контейнер). Каждый топик имеет JSON-схему сообщения, DLQ "
        "(dead letter queue) для необработанных сообщений и обязательный набор заголовков: "
        "eventId (UUID), eventType (строковый идентификатор типа события), schemaVersion "
        "(семантическая версия схемы), sourceService (имя сервиса-издателя), correlationId "
        "(идентификатор корреляции для трассировки), causationId (идентификатор причинного "
        "события) и occurredAtUtc (временная метка в формате ISO 8601)."
    )

    add_normal_paragraph(doc, "AI-модуль:", bold=True)
    add_normal_paragraph(
        doc,
        "AI-ассистент реализован на Python с использованием FastAPI и языковой модели "
        "Qwen 2.5-1.5B [19] в формате GGUF для локального инференса. Модель компактного размера "
        "(1.5 миллиарда параметров, ~1.2 ГБ в квантизации Q4_K_M) выбрана для обеспечения "
        "возможности развёртывания на сервере с ограниченными GPU-ресурсами. Рассматривались "
        "альтернативы: Llama 3.2-3B (более мощная, но требует 3+ ГБ VRAM), Phi-3 Mini "
        "(сравнимый размер, но менее развитая поддержка tool-use), Gemma 2-2B (хорошая "
        "производительность, но ограниченная мультиязычная поддержка). Qwen 2.5-1.5B "
        "выбрана благодаря отличной мультиязычной поддержке (русский, английский, казахский), "
        "встроенной поддержке tool-use формата и оптимальному балансу между размером и качеством."
    )

    add_normal_paragraph(doc, "Контейнеризация и развёртывание:", bold=True)
    add_normal_paragraph(
        doc,
        "Docker [27] используется для контейнеризации всех компонентов системы, обеспечивая "
        "воспроизводимость окружения, изоляцию зависимостей и портативность между средами "
        "разработки, тестирования и продуктива. Caddy выступает в роли reverse proxy, "
        "обеспечивая автоматическое TLS, HTTP/2 и маршрутизацию запросов к соответствующим "
        "хостам. CI/CD реализован через GitHub CI с четырьмя этапами: build (сборка "
        "Docker-образов с использованием BuildKit и layer caching), dotnet (компиляция "
        "и линтинг .NET-проектов), test (запуск юнит-тестов и проверок безопасности), "
        "deploy (развёртывание на продуктивном сервере через Ansible). Ansible "
        "автоматизирует полный цикл развёртывания: проверку предусловий, инициализацию "
        "баз данных, сборку образов, запуск контейнеров и настройку Cloudflare Tunnel."
    )

    # 1.4
    doc.add_paragraph("1.4 Обоснование архитектурных решений", style="Heading 2")

    add_normal_paragraph(
        doc,
        "Для серверной части платформы Collaby выбрана сервис-ориентированная архитектура "
        "(SOA) с готовностью к переходу на микросервисы (MSA-ready). Данный подход представляет "
        "собой компромисс между монолитной архитектурой и полноценной микросервисной архитектурой, "
        "оптимальный для команды из трёх разработчиков."
    )
    add_normal_paragraph(
        doc,
        "Монолитная архитектура предполагает размещение всей бизнес-логики в едином процессе "
        "и единой базе данных. К преимуществам монолита относятся простота разработки, отладки "
        "и развёртывания, а также отсутствие накладных расходов на сетевое взаимодействие между "
        "компонентами. Однако по мере роста кодовой базы монолит становится трудноуправляемым: "
        "изменения в одном модуле требуют пересборки и переразвёртывания всего приложения, "
        "масштабирование возможно только вертикально (увеличение ресурсов одного сервера), "
        "а отказ одного компонента приводит к отказу всей системы."
    )
    add_normal_paragraph(
        doc,
        "Микросервисная архитектура (MSA) предполагает разделение системы на множество независимых "
        "сервисов, каждый из которых обладает собственной базой данных, процессом и API. MSA "
        "обеспечивает горизонтальное масштабирование, изоляцию отказов и независимый деплой "
        "каждого сервиса. Однако MSA вводит значительную операционную сложность: необходимость "
        "service discovery, distributed tracing, circuit breakers, API gateway, а также "
        "увеличение количества сетевых вызовов и потенциальные проблемы consistency. Для "
        "команды из трёх разработчиков полноценная MSA создаёт несоразмерную операционную "
        "нагрузку."
    )

    headers_arch = ["Критерий", "Монолит", "SOA (MSA-ready)", "Микросервисы"]
    rows_arch = [
        ["Сложность разработки", "Низкая", "Средняя", "Высокая"],
        ["Изоляция отказов", "Отсутствует", "Частичная", "Полная"],
        ["Масштабируемость", "Вертикальная", "Гибридная", "Горизонтальная"],
        ["Размер команды", "1-3 чел.", "3-8 чел.", "8+ чел."],
        ["Стоимость инфраструктуры", "Низкая", "Средняя", "Высокая"],
        ["Возможность извлечения модулей", "Сложно", "Встроена", "Не требуется"],
        ["Общая кодовая база", "Единая", "Общие контракты", "Раздельная"],
        ["Количество процессов", "1", "4-5", "10+"],
        ["Сетевые вызовы", "Нет", "Минимальные", "Обширные"],
        ["Операционная сложность", "Низкая", "Средняя", "Высокая"],
    ]
    add_simple_table(doc, headers_arch, rows_arch, "Сравнение архитектурных подходов")

    add_normal_paragraph(
        doc,
        "Суть архитектуры SOA (MSA-ready) заключается в организации консолидированных хостов, "
        "каждый из которых содержит один или несколько доменных модулей. Хост access (порт 5001) "
        "объединяет IdentityService и UserService; workspace (порт 5003) содержит ManagementService "
        "и AnalyticsService; realtime (порт 5004) включает MessengerService и CallService; "
        "engagement (порт 5006) содержит GamificationService. Каждый модуль имеет собственный "
        "доменный слой, базу данных и API-контроллеры, но разделяет процесс и сетевой порт "
        "с другими модулями того же хоста."
    )
    add_normal_paragraph(
        doc,
        "Такая организация обеспечивает чёткие границы домена на уровне кода, при этом "
        "сохраняя простоту развёртывания и отладки для малой команды. При необходимости "
        "масштабирования любой модуль может быть извлечён из хоста в отдельный процесс "
        "без изменения контрактов, так как межмодульное взаимодействие уже реализовано "
        "через Kafka и HTTP API. Процесс извлечения модуля включает три шага: (1) создание "
        "отдельного проекта с собственным Program.cs и Dockerfile, (2) перенос маршрута "
        "в Caddyfile на новый порт, (3) обновление строки подключения к базе данных. "
        "Вся бизнес-логика, контракты событий и миграции остаются без изменений."
    )
    add_normal_paragraph(
        doc,
        "Общая кодовая база организована в виде монорепозитория с разделением на проекты: "
        "platform/contracts содержит общие контракты событий (IntegrationEvent, заголовки Kafka) "
        "и реестр топиков; platform/infrastructure содержит общие инфраструктурные компоненты "
        "(Kafka consumer/producer abstractions, Outbox pattern implementation, healthcheck "
        "endpoints); каждый сервис представлен отдельным проектом в каталоге services/ со "
        "слоями Domain, Application, Infrastructure и Web/Controllers."
    )

    add_normal_paragraph(
        doc,
        "В архитектуре применяются следующие паттерны проектирования распределённых систем."
    )
    add_normal_paragraph(
        doc,
        "Transactional Outbox [10]: каждый сервис записывает исходящие интеграционные события "
        "в локальную таблицу IntegrationOutboxMessage внутри той же транзакции, что и основные "
        "бизнес-данные. Фоновый диспетчер (OutboxDispatcher, реализованный как IHostedService) "
        "периодически (с настраиваемым интервалом, по умолчанию 1 секунда) извлекает необработанные "
        "сообщения из таблицы Outbox и публикует их в соответствующие топики Kafka. После успешной "
        "публикации сообщение помечается как обработанное (ProcessedAt = DateTime.UtcNow). Данный "
        "паттерн гарантирует, что события всегда согласованы с состоянием данных: если транзакция "
        "откатывается, событие не будет опубликовано; если сервис падает после фиксации транзакции, "
        "но до публикации в Kafka, событие будет опубликовано при следующем запуске диспетчера."
    )
    add_normal_paragraph(
        doc,
        "CQRS (Command Query Responsibility Segregation) [9]: AnalyticsService реализует модель "
        "чтения, потребляя события из Kafka-топиков management.task-events.v1, "
        "management.workspace-events.v1 и gamification.progress-events.v1, и формируя "
        "денормализованные проекции для аналитических запросов. Разделение модели записи "
        "(ManagementService) и модели чтения (AnalyticsService) позволяет оптимизировать "
        "каждую модель для своего сценария использования: ManagementService оптимизирован "
        "для транзакционных операций с нормализованными данными, а AnalyticsService - "
        "для быстрых аналитических запросов с предвычисленными агрегатами."
    )
    add_normal_paragraph(
        doc,
        "Event-driven Architecture: события ManagementService (task-completed, subtask-completed, "
        "comment-added, task-reopened) через Kafka поступают в GamificationService для начисления "
        "XP, обновления квестов и перестроения лидербордов. Данный подход обеспечивает слабую "
        "связанность между сервисами: ManagementService не знает о существовании GamificationService "
        "и публикует события безотносительно к их потребителям. Добавление нового потребителя "
        "(например, NotificationService) не требует изменений в издателе."
    )
    add_normal_paragraph(
        doc,
        "Saga [10, 11]: удаление пользователя реализовано через хореографическую Saga с использованием "
        "двух выделенных топиков: admin.user-deletion-commands.v1 (команды на удаление данных "
        "пользователя в каждом сервисе) и admin.user-deletion-results.v1 (результаты выполнения "
        "удаления). IdentityService выступает оркестратором: получив запрос на удаление, он "
        "публикует команду в топик commands, все сервисы (ManagementService, GamificationService, "
        "MessengerService) обрабатывают команду и публикуют результат в топик results. "
        "IdentityService собирает результаты и завершает процесс удаления. Данный паттерн "
        "обеспечивает согласованное удаление данных пользователя во всех сервисах без "
        "распределённых транзакций."
    )


# ---------------------------------------------------------------------------
# Section 2
# ---------------------------------------------------------------------------

def build_section_2(doc: Document):
    global _current_section
    _current_section = 2

    doc.add_paragraph("2 ПРОЕКТНО-ЭКСПЕРИМЕНТАЛЬНЫЙ РАЗДЕЛ", style="Heading 1")

    # 2.1
    doc.add_paragraph("2.1 Архитектура системы", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Общая архитектура платформы Collaby представляет собой набор консолидированных "
        "хостов, взаимодействующих через Apache Kafka (асинхронные события) и HTTP API "
        "(синхронные запросы). Фронтенд обслуживается Caddy, который одновременно выполняет "
        "роль reverse proxy, маршрутизируя API-запросы к соответствующим хостам."
    )

    # Architecture diagram
    diagram_data = _try_generate_architecture_diagram()
    if diagram_data:
        buf = BytesIO(diagram_data)
        doc.add_picture(buf, width=Cm(13))
        last_p = doc.paragraphs[-1]
        last_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        last_p.paragraph_format.first_line_indent = Cm(0)
    fig_num = _next_figure()
    p_cap = doc.add_paragraph()
    p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_cap.paragraph_format.first_line_indent = Cm(0)
    p_cap.paragraph_format.space_before = Pt(3)
    p_cap.paragraph_format.space_after = Pt(12)
    run_cap = p_cap.add_run(f"Рисунок {fig_num} - Компонентная диаграмма архитектуры Collaby")
    run_cap.font.name = FONT_NAME
    run_cap.font.size = Pt(12)

    add_normal_paragraph(
        doc,
        "Система состоит из пяти серверных хостов, каждый из которых запускается в отдельном "
        "Docker-контейнере. Хост access (порт 5001) содержит два доменных модуля: IdentityService "
        "(аутентификация, авторизация, управление токенами) и UserService (профили, аватары, "
        "предпочтения). Хост workspace (порт 5003) объединяет ManagementService (рабочие "
        "пространства, проекты, задачи, Kanban) и AnalyticsService (аналитические проекции, "
        "дашборды). Хост realtime (порт 5004) включает MessengerService (чаты, сообщения, "
        "присутствие) и CallService (WebRTC-сигнализация, управление сессиями). Хост engagement "
        "(порт 5006) содержит единственный модуль GamificationService (XP, квесты, магазин, "
        "лидерборды, серии, деревья навыков). Наконец, хост assistant (порт 8100) представляет "
        "собой Python-приложение на FastAPI с AI-моделью Qwen 2.5-1.5B."
    )
    add_normal_paragraph(
        doc,
        "Каждый .NET-хост организован по принципу вертикальных слайсов (Vertical Slices): "
        "модули внутри одного хоста разделяют процесс и HTTP-порт, но имеют изолированные "
        "пространства имён, DbContext-ы и Kafka-консюмеры. Общие компоненты (JWT-конфигурация, "
        "Kafka abstractions, healthcheck endpoints, middleware для rate limiting и error handling) "
        "вынесены в проект platform/infrastructure и подключаются как NuGet-пакет."
    )

    add_normal_paragraph(
        doc,
        "Маршрутизация API-запросов осуществляется на уровне Caddyfile. Каждый хост обслуживает "
        "определённый набор URL-префиксов, что обеспечивает прозрачность маршрутизации для "
        "фронтенда и возможность независимого масштабирования. Caddy автоматически проксирует "
        "WebSocket-соединения для SignalR Hub-ов, обеспечивая поддержку реального времени "
        "без дополнительной конфигурации."
    )

    headers_api = ["URL-префикс", "Целевой хост", "Сервис", "Протокол"]
    rows_api = [
        ["/api/auth/*", "access:5001", "IdentityService", "HTTP/JSON"],
        ["/api/users/*", "access:5001", "UserService", "HTTP/JSON"],
        ["/api/management/*", "workspace:5003", "ManagementService", "HTTP/JSON"],
        ["/api/analytics/*", "workspace:5003", "AnalyticsService", "HTTP/JSON"],
        ["/api/messenger/*", "realtime:5004", "MessengerService", "HTTP/JSON"],
        ["/api/calls/*", "realtime:5004", "CallService", "HTTP/JSON"],
        ["/api/gamification/*", "engagement:5006", "GamificationService", "HTTP/JSON"],
        ["/api/ai/*", "assistant:8100", "AgentService", "HTTP/JSON"],
        ["/hubs/management", "workspace:5003", "ManagementHub", "WebSocket"],
        ["/hubs/messenger", "realtime:5004", "MessengerHub", "WebSocket"],
        ["/hubs/calls", "realtime:5004", "CallHub", "WebSocket"],
        ["/hubs/gamification", "engagement:5006", "GamificationHub", "WebSocket"],
    ]
    add_simple_table(doc, headers_api, rows_api, "Маршрутизация API-запросов и WebSocket-соединений")

    add_normal_paragraph(
        doc,
        "Событийная модель системы построена на 9 типизированных топиках Apache Kafka, "
        "определённых в централизованном реестре topic-registry.json. Каждый топик имеет "
        "JSON-схему, DLQ (dead letter queue) для необработанных сообщений и обязательный "
        "набор заголовков (eventId, eventType, schemaVersion, sourceService, correlationId, "
        "causationId, occurredAtUtc). Консюмеры реализуют идемпотентную обработку через "
        "таблицу ProcessedIntegrationEvents, где фиксируется eventId каждого обработанного "
        "события, предотвращая повторную обработку при перебалансировке партиций."
    )

    headers_topics = ["Топик", "Издатель", "Подписчики", "Тип событий"]
    rows_topics = [
        ["identity.user-lifecycle.v1", "IdentityService", "UserService, ManagementService", "UserRegistered, UserDeleted, EmailVerified"],
        ["identity.user-directory.v1", "IdentityService", "UserService", "UserCreated (seed profile)"],
        ["user.profile-snapshot.v1", "UserService", "ManagementService, GamificationService", "ProfileUpdated, AvatarChanged"],
        ["management.task-events.v1", "ManagementService", "GamificationService, AnalyticsService", "TaskCompleted, SubtaskCompleted, CommentAdded, TaskReopened"],
        ["management.workspace-events.v1", "ManagementService", "AnalyticsService", "WorkspaceCreated, ProjectCreated, MemberJoined"],
        ["management.project-progress-snapshot.v1", "ManagementService", "GamificationService", "ProjectCompleted, ProgressSnapshot"],
        ["gamification.progress-events.v1", "GamificationService", "AnalyticsService", "XpAwarded, LevelUp, QuestCompleted, StreakUpdated"],
        ["admin.user-deletion-commands.v1", "IdentityService", "Management, Gamification, Messenger", "DeleteUserData"],
        ["admin.user-deletion-results.v1", "Все сервисы", "IdentityService", "UserDataDeleted, DeletionFailed"],
    ]
    add_simple_table(doc, headers_topics, rows_topics, "Событийная модель системы (Kafka-топики)")

    _insert_pillow_diagram(doc, _generate_event_flow_diagram(), "Диаграмма событийных потоков через Apache Kafka")

    add_normal_paragraph(
        doc,
        "Жизненный цикл интеграционного события включает следующие этапы. Сервис-издатель "
        "создаёт экземпляр IntegrationEvent с уникальным eventId (UUID v4), eventType "
        "(строковый идентификатор, например task-completed), schemaVersion (1.0.0), "
        "sourceService (имя издателя) и JSON-телом с данными события. Событие записывается "
        "в таблицу IntegrationOutboxMessage в той же транзакции, что и бизнес-данные. "
        "OutboxDispatcher извлекает необработанные сообщения, сериализует их в JSON "
        "с обязательными заголовками и публикует в соответствующий Kafka-топик. "
        "Консюмер получает событие, проверяет идемпотентность через ProcessedIntegrationEvents, "
        "десериализует тело и передаёт обработку соответствующему процессору. После успешной "
        "обработки eventId фиксируется в таблице ProcessedIntegrationEvents."
    )

    # 2.2
    doc.add_paragraph("2.2 Проектирование базы данных", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Система использует пять изолированных баз данных PostgreSQL, каждая из которых "
        "принадлежит определённому доменному модулю. Кроме того, MongoDB используется для "
        "хранения сообщений мессенджера и сессий звонков, а Redis выступает в роли кеша "
        "и хранилища временных данных. Изоляция баз данных на уровне PostgreSQL обеспечивается "
        "отдельными пользователями (roles) с ограниченными привилегиями: каждый пользователь "
        "имеет доступ только к своей базе данных."
    )

    add_normal_paragraph(doc, "База данных collaby_identity (IdentityService):", bold=True)
    add_normal_paragraph(
        doc,
        "База данных collaby_identity хранит учётные записи пользователей и данные аутентификации. "
        "Центральной сущностью является User, содержащая поля: Id (UUID, первичный ключ), "
        "Email (уникальный, до 256 символов), Username (уникальный, до 100 символов), "
        "PasswordHash (строка BCrypt, null для OAuth-пользователей), Provider (перечисление: "
        "Email, Google), IsActive (boolean, false до верификации email), IsAdmin (boolean), "
        "CreatedAt и UpdatedAt (временные метки UTC). Для сущности User определены индексы: "
        "уникальный по Email, уникальный по Username и фильтрованный по IsActive."
    )
    headers_id = ["Сущность", "Ключевые поля", "Индексы", "Назначение"]
    rows_id = [
        ["User", "Id, Email, Username, PasswordHash, Provider, IsActive, IsAdmin", "IX_Email (unique), IX_Username (unique)", "Учётные записи пользователей"],
        ["RefreshToken", "Id, UserId, Token, ExpiresAt, RevokedAt, CreatedByIp", "IX_Token (unique), IX_UserId", "Токены обновления JWT"],
        ["OAuthAccount", "Id, UserId, Provider, ExternalId, AccessToken", "IX_Provider_ExternalId (unique)", "Привязка OAuth-аккаунтов (Google)"],
        ["TwoFactorSetting", "UserId, SecretKey, IsEnabled, RecoveryCodes", "PK_UserId", "Настройки двухфакторной аутентификации"],
        ["EmailVerification", "Id, UserId, Token, ExpiresAt, VerifiedAt", "IX_Token (unique)", "Коды подтверждения email"],
        ["PasswordReset", "Id, UserId, Token, ExpiresAt, UsedAt", "IX_Token (unique)", "Запросы сброса пароля"],
        ["LoginAudit", "Id, UserId, IpAddress, UserAgent, IsSuccess, CreatedAt", "IX_UserId_CreatedAt", "Журнал входов в систему"],
        ["IntegrationOutboxMessage", "Id, EventType, Payload, CreatedAt, ProcessedAt", "IX_ProcessedAt (filtered: null)", "Исходящие интеграционные события"],
    ]
    add_simple_table(doc, headers_id, rows_id, "Сущности базы данных collaby_identity")

    _insert_pillow_diagram(doc, _generate_er_diagram_identity(), "ER-диаграмма базы данных collaby_identity")

    add_normal_paragraph(doc, "База данных collaby_users (UserService):", bold=True)
    add_normal_paragraph(
        doc,
        "База данных collaby_users хранит профили пользователей и их предпочтения. "
        "Сущность UserProfile содержит: UserId (UUID, первичный ключ, ссылка на User из "
        "IdentityService), DisplayName (отображаемое имя, до 100 символов), Bio (описание, "
        "до 500 символов), AvatarUrl (URL аватара в S3-совместимом хранилище), Timezone "
        "(часовой пояс, например Asia/Almaty), Locale (языковая настройка: ru, en, kz), "
        "Theme (тема оформления: light, dark, system), CreatedAt и UpdatedAt."
    )
    headers_user = ["Сущность", "Ключевые поля", "Назначение"]
    rows_user = [
        ["UserProfile", "UserId, DisplayName, Bio, AvatarUrl, Timezone, Locale, Theme", "Профиль пользователя"],
        ["UserPreference", "UserId, Key, Value", "Пользовательские предпочтения (key-value)"],
        ["ProcessedIntegrationEvents", "EventId, ProcessedAt", "Идемпотентность обработки событий"],
    ]
    add_simple_table(doc, headers_user, rows_user, "Сущности базы данных collaby_users")

    add_normal_paragraph(doc, "База данных collaby_management (ManagementService):", bold=True)
    add_normal_paragraph(
        doc,
        "База данных collaby_management является наиболее объёмной и содержит основную бизнес-логику "
        "управления проектами. Сущность Workspace представляет рабочее пространство с полями: Id (UUID), "
        "Name (до 100 символов), Description (до 1000 символов), OwnerId (UUID владельца), "
        "LogoUrl (URL логотипа), CreatedAt, UpdatedAt. Сущность WorkspaceMember связывает "
        "пользователя с рабочим пространством через составной ключ (WorkspaceId, UserId) "
        "и определяет роль: Owner, Admin или Member."
    )
    add_normal_paragraph(
        doc,
        "Сущность Project определяет проект внутри рабочего пространства: Id (UUID), "
        "WorkspaceId (FK), Name (до 200 символов), Description (до 2000 символов), "
        "CreatedBy (UUID), IsCompleted (boolean), CompletedAt (nullable), CreatedAt, UpdatedAt. "
        "Для каждого проекта автоматически создаются статусные колонки по умолчанию через "
        "сущность ProjectStatus: Id, ProjectId (FK), Name (до 50 символов, например Backlog, "
        "To Do, In Progress, Review, Done), Order (int, порядок отображения), Color (hex-цвет). "
        "Колонки могут быть добавлены, удалены или переименованы через API."
    )
    add_normal_paragraph(
        doc,
        "Сущность TaskItem является центральной для управления задачами: Id (UUID), ProjectId (FK), "
        "StatusId (FK на ProjectStatus), Title (до 500 символов), Description (до 5000 символов), "
        "Priority (перечисление: None, Low, Medium, High, Critical), AssigneeId (UUID, nullable), "
        "CreatorId (UUID), DueDate (nullable), Order (int, позиция в колонке), StickerColor "
        "(nullable, цвет стикера), CreatedAt, UpdatedAt. Для TaskItem определены индексы: "
        "IX_ProjectId_StatusId, IX_AssigneeId, IX_Priority. Подзадачи хранятся в SubTaskItem: "
        "Id, TaskId (FK), Title, IsCompleted, CompletedAt, Order."
    )
    headers_mgmt = ["Сущность", "Ключевые поля", "Назначение"]
    rows_mgmt = [
        ["Workspace", "Id, Name, Description, OwnerId, LogoUrl", "Рабочие пространства"],
        ["WorkspaceMember", "WorkspaceId, UserId, Role", "Участники пространства"],
        ["Project", "Id, WorkspaceId, Name, Description, CreatedBy, IsCompleted", "Проекты внутри пространства"],
        ["ProjectStatus", "Id, ProjectId, Name, Order, Color", "Статусные колонки Kanban"],
        ["TaskItem", "Id, ProjectId, StatusId, Title, Priority, AssigneeId, DueDate, Order, StickerColor", "Задачи"],
        ["SubTaskItem", "Id, TaskId, Title, IsCompleted, CompletedAt, Order", "Подзадачи"],
        ["TaskComment", "Id, TaskId, AuthorId, Content, CreatedAt", "Комментарии к задачам"],
        ["Notification", "Id, UserId, Type, Title, Message, IsRead, CreatedAt", "Уведомления пользователей"],
        ["ProjectLabel", "Id, ProjectId, Name, Color", "Метки проекта"],
        ["TaskLabel", "TaskId, LabelId", "Связь задач с метками (many-to-many)"],
        ["IntegrationOutboxMessage", "Id, EventType, Payload, CreatedAt, ProcessedAt", "Исходящие события"],
        ["ProcessedIntegrationEvents", "EventId, ProcessedAt", "Идемпотентность"],
    ]
    add_simple_table(doc, headers_mgmt, rows_mgmt, "Сущности базы данных collaby_management")

    _insert_pillow_diagram(doc, _generate_er_diagram_management(), "ER-диаграмма базы данных collaby_management")

    add_normal_paragraph(doc, "База данных collaby_analytics (AnalyticsService):", bold=True)
    add_normal_paragraph(
        doc,
        "База данных collaby_analytics содержит денормализованные проекции для аналитических "
        "запросов. Основные сущности включают: TaskActivityLog (журнал активности по задачам "
        "с полями UserId, ProjectId, ActionType, TaskId, OccurredAt), ProjectDailySummary "
        "(ежедневная сводка проекта: ProjectId, Date, TasksCreated, TasksCompleted, "
        "CommentsAdded, ActiveMembers), UserProductivityMetric (метрики продуктивности "
        "пользователя: UserId, ProjectId, Week, TasksCompleted, AverageCompletionTime, "
        "CommentsWritten). AnalyticsService не имеет таблицы IntegrationOutboxMessage, так "
        "как является исключительно потребителем событий и не публикует собственные события."
    )

    add_normal_paragraph(doc, "База данных collaby_gamification (GamificationService):", bold=True)
    add_normal_paragraph(
        doc,
        "База данных collaby_gamification хранит все данные геймификации и является одной из "
        "наиболее сложных по количеству сущностей. Подробное описание каждой сущности с "
        "указанием всех полей и их назначения приведено ниже."
    )
    add_normal_paragraph(
        doc,
        "Сущность UserGlobalXp хранит глобальный прогресс пользователя на платформе: UserId "
        "(UUID, первичный ключ), TotalXp (long, суммарный опыт), Level (int, текущий уровень, "
        "вычисляется по формуле), Coins (int, баланс виртуальной валюты), StreakDays (int, "
        "текущая длина серии активности), FreezeCount (int, количество заморозок серии в "
        "инвентаре), LastActivityDate (DateTime, дата последней активности для расчёта серии), "
        "CreatedAt, UpdatedAt. Уровень вычисляется по формуле: Level = 1 при TotalXp < 200, "
        "Level = max(2, TotalXp / 100) при TotalXp >= 200."
    )
    add_normal_paragraph(
        doc,
        "Сущность UserProjectXp хранит прогресс пользователя в рамках конкретного проекта: "
        "составной ключ (UserId, ProjectId), Xp (long, опыт в проекте), Level (int, уровень "
        "в проекте), TasksCompleted (int, количество завершённых задач), SubtasksCompleted "
        "(int, количество завершённых подзадач), CommentsAdded (int, количество комментариев), "
        "CreatedAt, UpdatedAt. Индекс IX_ProjectId_Xp используется для построения проектных "
        "лидербордов."
    )
    headers_gam = ["Сущность", "Ключевые поля", "Назначение"]
    rows_gam = [
        ["UserGlobalXp", "UserId, TotalXp, Level, Coins, StreakDays, FreezeCount, LastActivityDate", "Глобальный прогресс"],
        ["UserProjectXp", "UserId, ProjectId, Xp, Level, TasksCompleted, SubtasksCompleted, CommentsAdded", "Проектный прогресс"],
        ["XpTransaction", "Id, UserId, ProjectId, Amount, CoinsAmount, Reason, Status, TaskId, Metadata", "Журнал транзакций XP"],
        ["Quest", "Id, Type (Daily/Weekly), Title, Description, XpReward, CoinReward, Requirement (JSON)", "Каталог квестов"],
        ["UserQuest", "UserId, QuestId, Progress, WindowKey, IsCompleted, CompletedAt, ClaimedAt", "Прогресс квестов"],
        ["ShopItem", "Id, Name, Description, Category, Price, ImageUrl, IsPurchasable, IsConsumable", "Каталог магазина"],
        ["UserInventory", "Id, UserId, ItemId, IsEquipped, PurchasedAt, ConsumedAt", "Инвентарь пользователя"],
        ["LeaderboardSnapshot", "Id, Type (Global/Project), Year, Rank, UserId, Xp, SnapshotDate", "Годовые снимки лидерборда"],
        ["SkillTreeNode", "Id, ProjectId, Tier (1/2/3), Branch (A/B), Title, Description, RewardJson", "Узлы дерева навыков"],
        ["UserSkillTreeChoice", "Id, UserId, NodeId, ProjectId, ChosenAt", "Выбор навыков пользователем"],
        ["IntegrationOutboxMessage", "Id, EventType, Payload, CreatedAt, ProcessedAt", "Исходящие события"],
        ["ProcessedIntegrationEvents", "EventId, ProcessedAt", "Идемпотентность"],
    ]
    add_simple_table(doc, headers_gam, rows_gam, "Сущности базы данных collaby_gamification")

    _insert_pillow_diagram(doc, _generate_er_diagram_gamification(), "ER-диаграмма базы данных collaby_gamification")

    add_normal_paragraph(
        doc,
        "MongoDB используется тремя сервисами. MessengerService хранит коллекции в базе "
        "collaby_messenger: Chat (поля: _id, workspaceId, type (direct/group), name, "
        "participants (массив UUID), createdAt, updatedAt) и Message (поля: _id, chatId, "
        "senderId, content, type (text/system/file), attachments (массив), readBy (массив "
        "userId + readAt), createdAt, editedAt). Для коллекции Message создан составной "
        "индекс по chatId и createdAt для эффективной пагинации. CallService хранит коллекцию "
        "CallSession в базе collaby_calls: _id, workspaceId, initiatorId, participants (массив "
        "с userId, joinedAt, leftAt), status (active/ended), startedAt, endedAt."
    )
    add_normal_paragraph(
        doc,
        "Redis используется для кеширования (таблицы лидеров в виде sorted sets с ключами "
        "leaderboard:global и leaderboard:project:{projectId}), ограничения частоты запросов "
        "(rate limiting через скользящее окно с ключами rate:{userId}:{endpoint}), хранения "
        "данных присутствия пользователей (presence:{userId} с TTL 60 секунд) и временных "
        "данных сессий (session:{sessionId}). Для каждого сервиса настроена отдельная учётная "
        "запись Redis ACL с доступом только к определённым key patterns."
    )

    # 2.3
    doc.add_paragraph("2.3 Проектирование сервисных модулей", style="Heading 2")

    add_normal_paragraph(doc, "Access Host (порт 5001) - IdentityService:", bold=True)
    add_normal_paragraph(
        doc,
        "IdentityService отвечает за аутентификацию, авторизацию и управление жизненным циклом "
        "учётных записей. Модуль предоставляет следующие REST API эндпоинты: POST /api/auth/register "
        "(регистрация нового пользователя с валидацией email и username через FluentValidation, "
        "хешированием пароля через BCrypt и отправкой письма верификации через MailKit), "
        "POST /api/auth/login (аутентификация через email/пароль с генерацией JWT access token "
        "и refresh token, проверка 2FA если включена), POST /api/auth/google (OAuth 2.0 через "
        "Google: валидация id_token через Google.Apis.Auth, создание или привязка аккаунта), "
        "POST /api/auth/refresh (обновление access token через refresh token с ротацией), "
        "POST /api/auth/logout (отзыв refresh token), POST /api/auth/verify-email (верификация "
        "email через токен из письма), POST /api/auth/forgot-password (инициация сброса пароля), "
        "POST /api/auth/reset-password (сброс пароля через токен)."
    )
    add_normal_paragraph(
        doc,
        "Двухфакторная аутентификация реализована через эндпоинты: POST /api/auth/2fa/enable "
        "(генерация секретного ключа и QR-кода для Google Authenticator), POST /api/auth/2fa/verify "
        "(верификация TOTP-кода и активация 2FA), POST /api/auth/2fa/disable (отключение 2FA "
        "с предварительной верификацией текущего кода). JWT access token имеет срок жизни "
        "15 минут и содержит claims: sub (userId), email, username, role (user/admin). "
        "Refresh token хранится в HttpOnly Secure cookie с SameSite=Strict и сроком жизни "
        "7 дней. При каждом использовании refresh token выпускается новый токен (ротация), "
        "а старый помечается как отозванный."
    )

    add_normal_paragraph(doc, "Access Host (порт 5001) - UserService:", bold=True)
    add_normal_paragraph(
        doc,
        "UserService управляет профилями пользователей и предоставляет эндпоинты: GET /api/users/me "
        "(текущий профиль), PUT /api/users/me (обновление профиля: displayName, bio, timezone, "
        "locale, theme), POST /api/users/me/avatar (загрузка аватара с ресайзом через ImageSharp), "
        "GET /api/users/{userId} (профиль конкретного пользователя), GET /api/users/batch "
        "(пакетное получение профилей по массиву userId, используется другими сервисами для "
        "обогащения данных). UserService подписан на топик identity.user-lifecycle.v1 и "
        "автоматически создаёт профиль при регистрации нового пользователя."
    )

    add_normal_paragraph(doc, "Workspace Host (порт 5003) - ManagementService:", bold=True)
    add_normal_paragraph(
        doc,
        "ManagementService реализует основную бизнес-логику управления проектами. Модуль "
        "предоставляет обширный набор REST API эндпоинтов, организованных по контроллерам."
    )
    add_normal_paragraph(
        doc,
        "WorkspaceController: POST /api/management/workspaces (создание рабочего пространства), "
        "GET /api/management/workspaces (список пространств текущего пользователя), "
        "GET /api/management/workspaces/{id} (детали пространства), PUT /api/management/workspaces/{id} "
        "(обновление), DELETE /api/management/workspaces/{id} (удаление с каскадным удалением "
        "проектов), POST /api/management/workspaces/{id}/members (добавление участника по email "
        "с валидацией через IdentityService), DELETE /api/management/workspaces/{id}/members/{userId} "
        "(удаление участника), PUT /api/management/workspaces/{id}/members/{userId}/role (изменение "
        "роли участника)."
    )
    add_normal_paragraph(
        doc,
        "ProjectController: POST /api/management/workspaces/{wid}/projects (создание проекта "
        "с автоматическим созданием статусных колонок по умолчанию), GET .../projects (список "
        "проектов), GET .../projects/{pid} (детали с колонками и задачами), PUT .../projects/{pid} "
        "(обновление), DELETE .../projects/{pid} (удаление). TaskController: POST .../tasks "
        "(создание задачи с валидацией приоритета и статуса), GET .../tasks (список задач с "
        "фильтрацией по статусу, приоритету, исполнителю), PUT .../tasks/{tid} (обновление "
        "полей задачи), PATCH .../tasks/{tid}/status (перемещение задачи между колонками с "
        "публикацией события через Outbox и SignalR-нотификацией), DELETE .../tasks/{tid}, "
        "POST .../tasks/{tid}/subtasks (создание подзадачи), PATCH .../tasks/{tid}/subtasks/{sid} "
        "(завершение подзадачи), POST .../tasks/{tid}/comments (добавление комментария)."
    )
    add_normal_paragraph(
        doc,
        "ManagementService использует SignalR Hub (ManagementHub) для обновления Kanban-доски "
        "в реальном времени. При любом изменении задачи (создание, обновление, перемещение, "
        "удаление) Hub отправляет событие всем подключённым клиентам того же проекта. "
        "Группировка клиентов осуществляется по projectId через SignalR Groups."
    )

    add_normal_paragraph(doc, "Workspace Host (порт 5003) - AnalyticsService:", bold=True)
    add_normal_paragraph(
        doc,
        "AnalyticsService реализует паттерн CQRS: потребляет события из Kafka-топиков "
        "management.task-events.v1, management.workspace-events.v1 и gamification.progress-events.v1, "
        "формируя денормализованные проекции для аналитических панелей. Эндпоинты: "
        "GET /api/analytics/workspaces/{wid}/summary (сводка по рабочему пространству: "
        "количество проектов, задач, участников, активность за последние 30 дней), "
        "GET /api/analytics/projects/{pid}/activity (график активности по проекту: "
        "задачи созданные/завершённые по дням), GET /api/analytics/projects/{pid}/members "
        "(статистика по участникам: задачи завершённые, комментарии, среднее время выполнения), "
        "GET /api/analytics/users/me/productivity (персональная продуктивность: тренды, "
        "сравнение с предыдущими периодами)."
    )

    add_normal_paragraph(doc, "Realtime Host (порт 5004) - MessengerService:", bold=True)
    add_normal_paragraph(
        doc,
        "MessengerService обеспечивает обмен сообщениями в чатах. Эндпоинты: "
        "POST /api/messenger/chats (создание чата: direct или group), "
        "GET /api/messenger/chats (список чатов текущего пользователя с последним сообщением), "
        "GET /api/messenger/chats/{chatId}/messages (история сообщений с cursor-based пагинацией), "
        "POST /api/messenger/chats/{chatId}/messages (отправка сообщения с оповещением через "
        "SignalR MessengerHub), PUT /api/messenger/chats/{chatId}/messages/{msgId} (редактирование), "
        "DELETE /api/messenger/chats/{chatId}/messages/{msgId} (удаление). MessengerHub обеспечивает "
        "realtime доставку сообщений и уведомлений о печати (typing indicators). Присутствие "
        "пользователей (online/offline/away) отслеживается через Redis с TTL 60 секунд: "
        "при каждом действии пользователя обновляется ключ presence:{userId}."
    )

    add_normal_paragraph(doc, "Realtime Host (порт 5004) - CallService:", bold=True)
    add_normal_paragraph(
        doc,
        "CallService реализует WebRTC-сигнализацию для видеозвонков. Архитектура основана "
        "на peer-to-peer модели с SFU-подготовкой: SignalR CallHub обеспечивает обмен "
        "SDP-офферами (offer/answer) и ICE-кандидатами между участниками. Эндпоинты: "
        "POST /api/calls/sessions (инициация звонка с созданием CallSession в MongoDB), "
        "POST /api/calls/sessions/{sid}/join (присоединение к звонку), "
        "POST /api/calls/sessions/{sid}/leave (выход из звонка). CallHub методы: "
        "SendOffer (отправка SDP-оффера конкретному участнику), SendAnswer (отправка "
        "SDP-ответа), SendIceCandidate (отправка ICE-кандидата), EndCall (завершение "
        "звонка для всех участников)."
    )

    add_normal_paragraph(doc, "Engagement Host (порт 5006) - GamificationService:", bold=True)
    add_normal_paragraph(
        doc,
        "GamificationService реализует полную систему геймификации. Модуль предоставляет "
        "обширный набор API-эндпоинтов, организованных по контроллерам. XpController: "
        "GET /api/gamification/xp/global (глобальный прогресс текущего пользователя), "
        "GET /api/gamification/xp/project/{projectId} (проектный прогресс), "
        "GET /api/gamification/xp/transactions (история транзакций с пагинацией и фильтрацией "
        "по проекту и периоду). QuestController: GET /api/gamification/quests (текущие квесты "
        "пользователя с прогрессом), POST /api/gamification/quests/{questId}/claim (получение "
        "награды за завершённый квест). ShopController: GET /api/gamification/shop/items "
        "(каталог магазина с категориями), POST /api/gamification/shop/buy/{itemId} (покупка "
        "предмета), GET /api/gamification/shop/inventory (инвентарь пользователя), "
        "POST /api/gamification/shop/inventory/{inventoryId}/equip (экипировка предмета). "
        "LeaderboardController: GET /api/gamification/leaderboard/global (глобальный лидерборд), "
        "GET /api/gamification/leaderboard/project/{projectId} (проектный лидерборд). "
        "SkillTreeController: GET /api/gamification/skill-tree/{projectId} (дерево навыков проекта), "
        "POST /api/gamification/skill-tree/{projectId}/choose/{nodeId} (выбор навыка). "
        "StreakController: GET /api/gamification/streak (текущая серия пользователя). "
        "Подробное описание механик геймификации приведено в разделе 2.4."
    )

    add_normal_paragraph(doc, "Assistant (порт 8100) - AgentService:", bold=True)
    add_normal_paragraph(
        doc,
        "AgentService представляет собой Python-приложение на FastAPI, реализующее "
        "локальный AI-ассистент. Подробное описание приведено в разделе 2.5."
    )

    add_normal_paragraph(
        doc,
        "На рисунке ниже представлен интерфейс Kanban-доски, демонстрирующий "
        "визуальное управление задачами с колонками статусов, метками приоритетов "
        "и drag-and-drop перемещением."
    )
    add_figure_placeholder(doc, "Список задач пользователя с фильтрацией и метками", "kanban_board.png")

    add_normal_paragraph(
        doc,
        "Мессенджер платформы обеспечивает обмен сообщениями между участниками "
        "рабочего пространства в реальном времени через SignalR."
    )
    add_figure_placeholder(doc, "Мессенджер с чатами рабочего пространства", "messenger.png")

    add_normal_paragraph(
        doc,
        "Панель аналитики агрегирует данные о продуктивности команды, предоставляя "
        "графики активности и статистику по проектам."
    )
    add_figure_placeholder(doc, "Панель аналитики проекта", "analytics.png")

    # 2.4
    doc.add_paragraph("2.4 Система геймификации", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Система геймификации является центральным элементом научной новизны платформы Collaby. "
        "Она реализует комплексную игровую экономику, мотивирующую участников проектной "
        "деятельности через механики прогресса, достижений и социального сравнения. "
        "Проектирование системы основано на фреймворке Octalysis Ю Кай Чоу, типологии игроков "
        "Бартла и теории самодетерминации Деси и Райана, что обеспечивает научную обоснованность "
        "каждого игрового элемента."
    )

    add_normal_paragraph(doc, "2.4.1 Система опыта (XP) и уровней:", bold=True)
    add_normal_paragraph(
        doc,
        "XP-система реализует двухуровневую модель прогресса: глобальный XP (UserGlobalXp) "
        "отражает суммарный вклад пользователя по всей платформе, а проектный XP (UserProjectXp) "
        "фиксирует прогресс в рамках конкретного проекта. Данная двухуровневая модель адресует "
        "потребность в компетентности (SDT): пользователь видит свой прогресс как на уровне "
        "всей платформы, так и в контексте конкретного проекта."
    )
    add_normal_paragraph(
        doc,
        "Уровень пользователя вычисляется по формуле: Level = 1 при TotalXp < 200, "
        "Level = max(2, floor(TotalXp / 100)) при TotalXp >= 200. Порог следующего уровня "
        "определяется как XpForNextLevel(currentLevel) = (currentLevel + 1) * 100. Линейная "
        "прогрессия выбрана сознательно для обеспечения предсказуемости и справедливости: "
        "каждый уровень требует одинакового количества усилий. Прогресс текущего уровня "
        "отображается пользователю как процент заполнения шкалы опыта."
    )

    headers_xp = ["Действие (XpReason)", "Базовый XP", "Монеты", "Множитель приоритета", "Примечание"]
    rows_xp = [
        ["TaskCompleted", "10", "5", "Low=0.5, Medium=1.0, High=1.5, Critical=2.0", "Основной источник XP"],
        ["SubtaskCompleted", "3", "1", "Нет", "Фиксированная награда"],
        ["CommentAdded", "2", "0", "Нет", "Одна награда в день за задачу (дедупликация)"],
        ["QuestCompleted", "По квесту", "По квесту", "Нет", "XpReward и CoinReward из Quest"],
        ["StreakBonus", "0", "0", "Нет", "Реализуется через множитель streak"],
        ["ProjectCompleted", "0", "0", "Нет", "Зарезервировано для будущей реализации"],
        ["SkillTreeReward", "По узлу", "По узлу", "Нет", "RewardJson из SkillTreeNode"],
        ["AnnualAward", "0", "0", "Нет", "Бейджи за топ-3 лидерборда"],
        ["TaskReopened", " -XP", " -coins", "Обратный", "Откат: отрицательная транзакция"],
    ]
    add_simple_table(doc, headers_xp, rows_xp, "Награды за действия в системе XP")

    add_normal_paragraph(
        doc,
        "Вычисление уровня и множителя серии реализовано в XpService следующим образом:"
    )
    add_code_block(doc, """public static int CalculateLevel(long totalXp)
 => totalXp < 200 ? 1 : Math.Max(2, (int)(totalXp / 100));

public static decimal GetStreakMultiplier(int streakDays)
 => Math.Min(1.5m, 1.0m + (streakDays / 7) * 0.1m);""",
        "Листинг 1 - Вычисление уровня и множителя серии (XpService.cs)")

    add_normal_paragraph(
        doc,
        "Процесс начисления XP реализован в XpService.AwardXpAsync. Метод принимает userId, "
        "projectId, reason (перечисление XpReason), taskId и метаданные. Алгоритм начисления: "
        "(1) вычисление базового XP на основе reason; (2) применение множителя приоритета "
        "(для TaskCompleted - приоритет задачи: Low=0.5x, Medium=1.0x, High=1.5x, "
        "Critical=2.0x); (3) применение множителя серии активности (streak multiplier); "
        "(4) создание иммутабельной записи XpTransaction со статусом Completed; (5) обновление "
        "UserGlobalXp.TotalXp и пересчёт Level; (6) обновление UserProjectXp.Xp и пересчёт "
        "Level; (7) обновление Coins в UserGlobalXp (множитель streak к монетам не применяется); "
        "(8) перестроение лидербордов в Redis."
    )
    add_normal_paragraph(
        doc,
        "Ко всем XP-наградам применяется множитель серии активности (streak multiplier). "
        "Множитель равен 1.0 при серии менее 7 дней и увеличивается на 0.1 за каждые 7 дней "
        "серии до максимума 1.5x (при серии 35+ дней). Формула: streakMultiplier = "
        "min(1.5, 1.0 + floor(StreakDays / 7) * 0.1). К монетам множитель не применяется, "
        "чтобы экономика магазина оставалась предсказуемой и не создавала чрезмерного "
        "преимущества для пользователей с длинными сериями."
    )

    headers_streak_mult = ["Длина серии (дней)", "Множитель XP", "Бонус XP за TaskCompleted (Medium)"]
    rows_streak_mult = [
        ["0-6", "1.0x", "10 XP"],
        ["7-13", "1.1x", "11 XP"],
        ["14-20", "1.2x", "12 XP"],
        ["21-27", "1.3x", "13 XP"],
        ["28-34", "1.4x", "14 XP"],
        ["35+", "1.5x (максимум)", "15 XP"],
    ]
    add_simple_table(doc, headers_streak_mult, rows_streak_mult, "Множитель серии активности")

    add_normal_paragraph(
        doc,
        "Обработка отката XP при переоткрытии задачи (TaskReopened) реализована через "
        "отрицательную транзакцию. XpService находит оригинальную транзакцию по TaskId "
        "и Reason=TaskCompleted, создаёт новую транзакцию с отрицательными значениями XP "
        "и монет, обновляет UserGlobalXp и UserProjectXp. Это обеспечивает полную "
        "прозрачность истории транзакций и предотвращает злоупотребление через многократное "
        "завершение и переоткрытие задач."
    )

    add_normal_paragraph(doc, "2.4.2 Система квестов:", bold=True)
    add_normal_paragraph(
        doc,
        "Квесты бывают двух типов: ежедневные (Daily) и еженедельные (Weekly). Каждый квест "
        "определяет условие выполнения в формате JSON (поле Requirement), например "
        '{"type":"complete_tasks","count":3} или {"type":"maintain_streak","count":7}. '
        "Квесты назначаются пользователям лениво при первом запросе или событии в текущем "
        "временном окне. Ключ окна (WindowKey) формируется как дата для ежедневных квестов "
        "(yyyy-MM-dd) и ISO-неделя для еженедельных (yyyy-Wnn). При смене окна прогресс "
        "квеста сбрасывается автоматически."
    )
    add_normal_paragraph(
        doc,
        "Каталог квестов определяет следующие типы условий: complete_tasks (завершить N задач), "
        "complete_subtasks (завершить N подзадач), add_comments (добавить N комментариев), "
        "complete_high_priority (завершить N задач с приоритетом High или Critical), "
        "maintain_streak (поддерживать серию N дней), earn_xp (заработать N XP). "
        "Для каждого типа условия определён соответствующий обработчик в QuestService, "
        "который сопоставляет входящее событие с требованиями квеста."
    )

    headers_quests = ["Тип квеста", "Название", "Условие", "Награда XP", "Награда Coins"]
    rows_quests = [
        ["Daily", "Продуктивный день", "Завершить 3 задачи", "15", "10"],
        ["Daily", "Командный игрок", "Добавить 5 комментариев", "10", "5"],
        ["Daily", "Приоритетная работа", "Завершить 1 задачу High/Critical", "20", "15"],
        ["Weekly", "Марафонец", "Завершить 15 задач за неделю", "50", "30"],
        ["Weekly", "Серийный работник", "Поддерживать серию 7 дней", "40", "25"],
        ["Weekly", "Мастер подзадач", "Завершить 20 подзадач за неделю", "35", "20"],
        ["Weekly", "Опытный специалист", "Заработать 100 XP за неделю", "30", "20"],
    ]
    add_simple_table(doc, headers_quests, rows_quests, "Каталог квестов геймификации")

    add_normal_paragraph(
        doc,
        "Обработка прогресса квестов выполняется в QuestService.UpdateProgressAsync, "
        "который сопоставляет тип входящего события с требованиями каждого активного квеста "
        "пользователя. Алгоритм: (1) получить все активные (незавершённые) квесты пользователя "
        "текущего окна; (2) для каждого квеста десериализовать Requirement JSON; (3) проверить, "
        "соответствует ли тип события типу условия квеста; (4) если да - инкрементировать "
        "Progress на 1; (5) если Progress >= Requirement.Count - пометить квест как "
        "завершённый (IsCompleted = true, CompletedAt = DateTime.UtcNow); (6) отправить "
        "SignalR-уведомление QuestProgressUpdated или QuestCompleted."
    )
    add_normal_paragraph(
        doc,
        "Завершённый квест требует ручного получения награды (claim) через эндпоинт "
        "POST /api/gamification/quests/{questId}/claim. Это обеспечивает осознанное вовлечение "
        "пользователя: вместо автоматического начисления награда получается через явное действие, "
        "что создаёт дополнительный момент удовлетворения (reinforcement). При claim "
        "QuestService вызывает XpService.AwardXpAsync с Reason=QuestCompleted и наградами "
        "из Quest.XpReward и Quest.CoinReward. ClaimedAt фиксируется в UserQuest."
    )

    add_normal_paragraph(doc, "2.4.3 Система магазина:", bold=True)
    add_normal_paragraph(
        doc,
        "Виртуальный магазин позволяет пользователям приобретать предметы за монеты (coins), "
        "заработанные через выполнение задач и квестов. Магазин реализует драйвер Ownership & "
        "Possession из Octalysis Framework: пользователи формируют привязанность к своей "
        "коллекции предметов и стремятся расширять её. Каталог включает четыре категории "
        "предметов, определённых в перечислении ShopItemCategory."
    )

    headers_shop = ["Категория", "Описание", "Примеры", "Расходуемость", "Ценовой диапазон"]
    rows_shop = [
        ["Frames", "Рамки для аватара", "Golden Frame, Neon Frame, Diamond Frame", "Нет", "20-50 coins"],
        ["Themes", "Темы оформления интерфейса", "Dark Pro, Ocean Blue, Forest Green", "Нет", "30-80 coins"],
        ["Badges", "Значки для профиля", "Early Adopter, Code Master, Team Player", "Нет", "15-40 coins"],
        ["Consumables", "Расходуемые предметы", "Streak Freeze (заморозка серии), XP Boost (бонус XP)", "Да", "10-25 coins"],
    ]
    add_simple_table(doc, headers_shop, rows_shop, "Категории предметов магазина")

    add_normal_paragraph(
        doc,
        "Процесс покупки реализован в ShopService.PurchaseItemAsync. Алгоритм: (1) загрузить "
        "ShopItem по itemId и проверить IsPurchasable; (2) если предмет нерасходуемый "
        "(IsConsumable=false) - проверить, что пользователь ещё не владеет этим предметом "
        "через UserInventory; (3) проверить достаточность баланса: UserGlobalXp.Coins >= "
        "ShopItem.Price; (4) в единой транзакции: списать монеты (UserGlobalXp.Coins -= Price), "
        "создать запись UserInventory (UserId, ItemId, IsEquipped=false, PurchasedAt=UtcNow). "
        "Атомарность транзакции гарантирует, что при сбое не произойдёт ни списания монет "
        "без добавления предмета, ни добавления предмета без списания."
    )
    add_normal_paragraph(
        doc,
        "Экипировка предметов осуществляется через PATCH /api/gamification/shop/inventory/{id}/equip. "
        "Для нерасходуемых предметов экипировка переключает IsEquipped и применяет визуальный "
        "эффект (рамка аватара, тема интерфейса, значок в профиле). Для расходуемых предметов "
        "экипировка означает использование: Streak Freeze добавляет +1 к UserGlobalXp.FreezeCount, "
        "а запись UserInventory помечается ConsumedAt. Нерасходуемые предметы нельзя приобрести "
        "повторно; расходуемые можно приобретать многократно."
    )

    add_normal_paragraph(doc, "2.4.4 Таблицы лидеров (Leaderboards):", bold=True)
    add_normal_paragraph(
        doc,
        "Система поддерживает два типа лидербордов: глобальный (по TotalXp из UserGlobalXp) "
        "и проектный (по Xp из UserProjectXp). Лидерборды реализуют драйвер Social Influence "
        "из Octalysis и адресуют архетип Killer из типологии Бартла: пользователи видят "
        "свою позицию относительно других участников, что стимулирует соревновательную мотивацию."
    )
    add_normal_paragraph(
        doc,
        "Для ускорения чтения используется Redis sorted set: ключ leaderboard:global хранит "
        "пары (userId, TotalXp), ключ leaderboard:project:{projectId} хранит пары (userId, Xp). "
        "При каждом начислении XP или выполнении квеста LeaderboardService полностью перестраивает "
        "соответствующий лидерборд в Redis из PostgreSQL. Алгоритм перестроения: (1) выполнить "
        "запрос SELECT UserId, TotalXp FROM UserGlobalXp ORDER BY TotalXp DESC; (2) удалить "
        "текущий sorted set; (3) выполнить ZADD с массивом (score=TotalXp, member=UserId). "
        "Чтение лидерборда выполняется через ZREVRANGE с пагинацией. Если Redis недоступен, "
        "запрос выполняется напрямую к PostgreSQL как fallback."
    )
    add_normal_paragraph(
        doc,
        "Ежегодно (1 января) создаётся замороженный снимок (LeaderboardSnapshot) глобального "
        "лидерборда. Снимок фиксирует Rank, UserId и Xp для каждого пользователя. Три лучших "
        "пользователя автоматически получают бейджи: Gold Crown (1-е место), Silver Crown "
        "(2-е место), Bronze Crown (3-е место). Бейджи добавляются в инвентарь через "
        "ShopService и являются эксклюзивными (непокупаемыми через магазин)."
    )

    add_normal_paragraph(doc, "2.4.5 Серии активности (Streaks):", bold=True)
    add_normal_paragraph(
        doc,
        "Серия активности отслеживает последовательность дней, в которые пользователь "
        "завершал хотя бы одну задачу. Серии реализуют драйвер Loss & Avoidance из Octalysis: "
        "пользователь стремится не потерять накопленную серию, что стимулирует ежедневную "
        "активность. Данная механика аналогична серии в Duolingo, которая доказала свою "
        "эффективность для поддержания регулярной активности."
    )
    add_normal_paragraph(
        doc,
        "StreakService.RecordActivityAsync вызывается только при событии TaskCompleted, "
        "чтобы серия оставалась привязанной к полезной активности (а не к любому входу "
        "в систему). Алгоритм: (1) загрузить UserGlobalXp; (2) определить дату последней "
        "активности (LastActivityDate); (3) если LastActivityDate == сегодня - серия "
        "уже зачтена, выход; (4) если LastActivityDate == вчера - инкрементировать "
        "StreakDays, обновить LastActivityDate; (5) если LastActivityDate < вчера - "
        "рассчитать количество пропущенных дней (gap = (today - LastActivityDate).Days - 1)."
    )
    add_normal_paragraph(
        doc,
        "Если пользователь пропустил один или несколько дней, активируется механика заморозки "
        "(streak freeze). Алгоритм: (1) если FreezeCount >= gap (количество заморозок "
        "достаточно для покрытия пропуска), то FreezeCount -= gap, StreakDays += (gap + 1), "
        "LastActivityDate = today; (2) если FreezeCount < gap (заморозок недостаточно), "
        "то StreakDays = 1, FreezeCount = 0, LastActivityDate = today (серия начинается "
        "заново). Заморозки приобретаются в магазине (ShopItem категории Consumables) "
        "и расходуются автоматически при пропуске дня."
    )
    add_normal_paragraph(
        doc,
        "Для предотвращения злоупотреблений серия зачитывается только при реальном завершении "
        "задачи (событие TaskCompleted из Kafka), а не при любом действии в системе. Это "
        "гарантирует, что серия отражает реальную продуктивность, а не просто факт входа "
        "в систему. Множитель серии (streak multiplier) применяется ко всем XP-наградам, "
        "создавая дополнительный стимул для поддержания непрерывной активности."
    )

    add_normal_paragraph(doc, "2.4.6 Деревья навыков (Skill Trees):", bold=True)
    add_normal_paragraph(
        doc,
        "Деревья навыков привязаны к проектам и позволяют участникам выбирать направление "
        "специализации. Данная механика реализует драйвер Empowerment of Creativity из "
        "Octalysis и адресует потребность в автономии из SDT: пользователь принимает "
        "стратегические решения о своём развитии в проекте."
    )
    add_normal_paragraph(
        doc,
        "Каждый узел (SkillTreeNode) принадлежит определённому проекту, слою (Tier 1, 2, 3) "
        "и ветке (Branch A, B). Структура дерева: Tier 1 содержит один корневой узел "
        "(доступен сразу), Tier 2 содержит два узла (ветки A и B, выбор одного исключает "
        "другой), Tier 3 содержит два узла (продолжение выбранной ветки и финальная "
        "специализация). Выбор узла возможен только после достижения определённого уровня "
        "в проекте (UserProjectXp.Level >= Tier * 2)."
    )
    add_normal_paragraph(
        doc,
        "Каждый узел содержит JSON-описание награды (RewardJson). Типы наград: "
        '{"xpMultiplier": 1.1} - постоянный бонус к XP в данном проекте; '
        '{"coinBonus": 2} - дополнительные монеты за каждую задачу; '
        '{"badge": "specialist"} - эксклюзивный значок; '
        '{"title": "Архитектор"} - специальное звание в профиле. '
        "Выбор узла фиксируется в UserSkillTreeChoice и является необратимым, что "
        "придаёт решению значимость и стимулирует вдумчивый выбор."
    )

    headers_skill = ["Tier", "Ветка", "Название", "Требование", "Награда"]
    rows_skill = [
        ["1", " -", "Основы", "Level 2", "xpMultiplier: 1.05"],
        ["2", "A", "Исполнитель", "Level 4", "coinBonus: 2 за TaskCompleted"],
        ["2", "B", "Коммуникатор", "Level 4", "xpMultiplier: 1.1 за CommentAdded"],
        ["3", "A", "Мастер задач", "Level 6", "badge: task-master, title: Мастер"],
        ["3", "B", "Наставник", "Level 6", "badge: mentor, title: Наставник"],
    ]
    add_simple_table(doc, headers_skill, rows_skill, "Пример дерева навыков проекта")

    add_normal_paragraph(doc, "2.4.7 Событийный конвейер геймификации:", bold=True)
    add_normal_paragraph(
        doc,
        "Полный цикл обработки игрового события выглядит следующим образом. Этап 1: "
        "ManagementService при завершении задачи (изменение StatusId на колонку Done) "
        "записывает событие TaskCompleted в таблицу IntegrationOutboxMessage в той же "
        "транзакции, что и обновление статуса задачи. Событие содержит JSON-тело: "
        "taskId, projectId, assigneeId, priority, completedAt."
    )
    add_normal_paragraph(
        doc,
        "Этап 2: фоновый OutboxDispatcher (GamificationOutboxDispatcher, IHostedService) "
        "с интервалом 1 секунда извлекает необработанные сообщения из таблицы Outbox, "
        "сериализует их с обязательными заголовками (eventId, eventType, schemaVersion, "
        "sourceService, correlationId, causationId, occurredAtUtc) и публикует в топик "
        "management.task-events.v1."
    )
    add_normal_paragraph(
        doc,
        "Этап 3: ManagementTaskEventsConsumer в GamificationService получает событие из Kafka, "
        "проверяет идемпотентность через таблицу ProcessedIntegrationEvents (если eventId "
        "уже обработан - пропускает) и передаёт обработку ManagementTaskEventProcessor."
    )
    add_normal_paragraph(
        doc,
        "Этап 4: ManagementTaskEventProcessor в единой транзакции выполняет: (а) начисление XP "
        "через XpService.AwardXpAsync с учётом приоритета задачи и множителя серии; "
        "(б) обновление прогресса квестов через QuestService.UpdateProgressAsync; "
        "(в) запись активности серии через StreakService.RecordActivityAsync; "
        "(г) перестроение лидербордов через LeaderboardService.RebuildAsync; "
        "(д) запись исходящего события gamification.progress-events.v1 для AnalyticsService "
        "через Outbox."
    )
    add_normal_paragraph(
        doc,
        "Этап 5: после фиксации транзакции GamificationHub отправляет SignalR-уведомления "
        "пользователю: RewardReceived (с данными о начисленных XP и монетах), "
        "LevelUp (если уровень повысился), QuestProgressUpdated (если прогресс квеста "
        "изменился), QuestCompleted (если квест завершён), StreakUpdated (если серия "
        "обновилась). Фронтенд отображает анимированные уведомления через компоненты "
        "RewardNotification и LevelUpAnimation."
    )
    add_normal_paragraph(
        doc,
        "Ключевой метод обработчика ManagementTaskEventProcessor:"
    )
    add_code_block(doc, """public async Task ProcessTaskCompleted(TaskCompletedEvent e)
{
 await _xpService.AwardXpAsync(e.AssigneeId, e.ProjectId,
 XpReason.TaskCompleted, e.TaskId, e.Priority);
 await _questService.UpdateProgressAsync(e.AssigneeId, "complete_tasks");
 await _streakService.RecordActivityAsync(e.AssigneeId);
 await _leaderboardService.RebuildAsync(e.ProjectId);
}""",
        "Листинг 2 - Обработка события TaskCompleted (фрагмент)")

    add_normal_paragraph(
        doc,
        "Данный событийный конвейер обеспечивает полную согласованность данных: все операции "
        "геймификации выполняются в единой транзакции, Transactional Outbox гарантирует "
        "доставку событий, идемпотентность через ProcessedIntegrationEvents предотвращает "
        "повторную обработку, а SignalR обеспечивает мгновенную обратную связь пользователю. "
        "Среднее время от завершения задачи до получения уведомления о награде составляет "
        "менее 2 секунд."
    )

    _insert_pillow_diagram(doc, _generate_gamification_flow_diagram(), "Конвейер начисления XP при завершении задачи")

    add_normal_paragraph(
        doc,
        "На рисунке ниже представлен интерфейс страницы геймификации, включающий "
        "текущий уровень и прогресс XP, активные квесты, лидерборд и серию дней."
    )
    add_figure_placeholder(doc, "Страница геймификации (прогресс, квесты, лидерборд)", "gamification.png")

    # 2.5
    doc.add_paragraph("2.5 AI-ассистент", style="Heading 2")
    add_normal_paragraph(
        doc,
        "AI-ассистент реализован как отдельный Python-сервис (AgentService) на базе FastAPI, "
        "использующий языковую модель Qwen 2.5-1.5B в формате GGUF для локального инференса. "
        "Выбор компактной модели обусловлен необходимостью развёртывания на сервере с "
        "ограниченными GPU-ресурсами без обращения к внешним API, что обеспечивает "
        "конфиденциальность проектных данных."
    )
    add_normal_paragraph(
        doc,
        "Модель Qwen 2.5-1.5B выбрана на основании следующих критериев. Размер: 1.5 миллиарда "
        "параметров, ~1.2 ГБ в квантизации Q4_K_M формата GGUF, что позволяет загрузить модель "
        "в GPU с 4+ ГБ VRAM или работать на CPU с приемлемой скоростью (~15 tokens/sec на CPU, "
        "~60 tokens/sec на GPU NVIDIA T4). Мультиязычность: Qwen 2.5 обучен на корпусах "
        "русского, английского, китайского и других языков, что критически важно для "
        "казахстанских пользователей. Tool-use: модель поддерживает формат вызова инструментов "
        "(function calling), что позволяет реализовать интеграцию с контекстом рабочего "
        "пространства через структурированные запросы к ManagementService API."
    )
    add_normal_paragraph(
        doc,
        "Архитектура AgentService включает следующие компоненты. FastAPI-приложение (main.py) "
        "определяет HTTP-эндпоинты: POST /api/ai/chat (основной чат с учётом контекста "
        "проекта), POST /api/ai/follow-up (генерация follow-up предложений на основе "
        "предыдущего ответа), POST /api/ai/mentor (менторский режим с поддержкой транскрипции "
        "аудио записей встреч и генерации черновиков задач), POST /api/ai/workspace-assistant "
        "(ассистент рабочего пространства с поиском по базе знаний), "
        "POST /api/ai/sync-context (синхронизация контекста проекта из ManagementService)."
    )
    add_normal_paragraph(
        doc,
        "AgentService (agent.py) реализует основную логику инференса. Процесс обработки "
        "запроса включает: (1) валидация JWT-токена через общий секретный ключ "
        "(IDENTITY_JWT_KEY); (2) извлечение userId и проверка прав доступа; (3) загрузка "
        "контекста проекта из кеша или запрос к ManagementService через внутренний API-ключ "
        "(INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY): список задач, колонки, участники, метки; "
        "(4) формирование system prompt с инъекцией контекста проекта; (5) добавление "
        "истории разговора (до 10 последних сообщений); (6) инференс модели с поддержкой "
        "tool-use схем; (7) если модель запросила инструмент - выполнение вызова "
        "и повторный инференс с результатом; (8) возврат ответа с метаданными."
    )
    add_normal_paragraph(
        doc,
        "Tool-use схемы определяют инструменты, доступные модели: get_project_tasks "
        "(получение списка задач проекта с фильтрацией по статусу и приоритету), "
        "get_task_details (детальная информация о конкретной задаче с подзадачами "
        "и комментариями), get_team_members (список участников проекта с ролями), "
        "create_task_draft (создание черновика задачи для последующего подтверждения "
        "пользователем), get_project_statistics (статистика проекта: количество задач "
        "по статусам, активность за период). Каждый инструмент определяет JSON-схему "
        "параметров и возвращаемого значения."
    )
    add_normal_paragraph(
        doc,
        "Менторский режим (POST /api/ai/mentor) предназначен для анализа записей встреч. "
        "Пользователь загружает аудиозапись или текстовую транскрипцию встречи, и ассистент "
        "анализирует содержание, выделяя ключевые решения, назначенные ответственные и "
        "сроки. На основе анализа генерируются черновики задач в формате, совместимом "
        "с ManagementService API: title, description, priority, assigneeId (если упомянут "
        "конкретный участник). Черновики требуют подтверждения пользователем перед созданием "
        "реальных задач."
    )
    add_normal_paragraph(
        doc,
        "Аутентификация запросов выполняется через валидацию JWT-токенов, полученных от "
        "IdentityService. Сервис принимает тот же JWT, что и все остальные сервисы платформы, "
        "используя общий секретный ключ (IDENTITY_JWT_KEY). Для доступа к данным из "
        "ManagementService и UserService используются внутренние API-ключи "
        "(INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY, INTERNAL_AUTH_AI_TO_USER_KEY), передаваемые "
        "в заголовке X-Internal-Auth-Key. Система наблюдаемости реализована через "
        "OpenTelemetry с экспортом трассировок и метрик в OTLP-совместимый бэкенд "
        "(Grafana Tempo для трассировок, Prometheus для метрик). Метрики включают: "
        "inference_duration_seconds (время инференса), tokens_generated_total (количество "
        "сгенерированных токенов), tool_calls_total (количество вызовов инструментов)."
    )

    add_normal_paragraph(
        doc,
        "На рисунке ниже представлен интерфейс AI-ассистента, интегрированного "
        "в рабочее пространство платформы."
    )
    add_figure_placeholder(doc, "AI-ассистент с контекстом проекта", "ai_assistant.png")

    # 2.6
    doc.add_paragraph("2.6 Проектирование пользовательского интерфейса", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Клиентская часть платформы Collaby реализована как одностраничное приложение (SPA) "
        "на React 19.2.4 с использованием сборщика Vite 8.0.1 и TailwindCSS 4.2.2. "
        "Интерфейс разделён на три основные зоны: публичный лендинг (Landing), защищённая "
        "платформа (Platform) и административная панель (Admin). Маршрутизация реализована "
        "через React Router 7 с поддержкой защищённых маршрутов (ProtectedRoute), "
        "перенаправления неаутентифицированных пользователей и сохранения целевого URL."
    )
    add_normal_paragraph(
        doc,
        "Публичная часть (Landing) представляет информацию о платформе с анимированными "
        "секциями (Hero, Features, Pricing, Testimonials, FAQ) и содержит формы аутентификации: "
        "Auth (регистрация и вход с переключением между формами), VerifyEmail (подтверждение "
        "email по токену из письма), ForgotPassword (запрос сброса пароля), ResetPassword "
        "(установка нового пароля). Формы реализуют клиентскую валидацию с визуальной обратной "
        "связью, поддержку OAuth 2.0 через Google (кнопка Sign in with Google), а также "
        "двухфакторную аутентификацию (ввод TOTP-кода после успешного входа)."
    )
    add_normal_paragraph(
        doc,
        "Защищённая платформа (Platform) доступна только аутентифицированным пользователям "
        "и включает Layout с боковой навигацией (Sidebar) и верхней панелью (Topbar). "
        "Sidebar содержит навигацию по разделам текущего рабочего пространства: мессенджер "
        "(messenger), входящие уведомления (inbox), задачи (tasks - Kanban-доска), "
        "команда (team - список участников с ролями), аналитика (analytics - "
        "графики активности), магазин (shop - виртуальный магазин геймификации), "
        "награды (awards - инвентарь и экипировка), геймификация (gamification - "
        "прогресс, квесты, лидерборды, серии, дерево навыков) и профиль (profile - "
        "настройки аккаунта)."
    )
    add_normal_paragraph(
        doc,
        "Kanban-доска является центральным элементом интерфейса управления задачами. "
        "Реализация включает: отображение статусных колонок (ProjectStatus) с задачами "
        "(TaskItem), drag-and-drop перемещение задач между колонками через dnd-kit "
        "(useDraggable, useDroppable, DndContext), обновление порядка задач при перетаскивании, "
        "модальное окно создания/редактирования задачи (TaskModal) с полями title, description, "
        "priority, assignee, dueDate, labels, stickerColor, а также список подзадач "
        "(SubTaskList) с чекбоксами завершения. Все изменения синхронизируются в реальном "
        "времени через SignalR ManagementHub: при перемещении задачи одним пользователем "
        "все остальные участники проекта мгновенно видят обновлённую доску."
    )

    _insert_pillow_diagram(doc, _generate_ui_sitemap_diagram(), "Карта страниц SPA-приложения Collaby")

    add_normal_paragraph(
        doc,
        "На рисунках ниже представлены ключевые экраны публичной и защищённой "
        "частей интерфейса платформы."
    )
    add_figure_placeholder(doc, "Публичный лендинг платформы Collaby", "landing.png")
    add_figure_placeholder(doc, "Страница аутентификации (вход в систему)", "auth.png")
    add_figure_placeholder(doc, "Дашборд рабочего пространства с боковой навигацией", "dashboard.png")

    add_normal_paragraph(
        doc,
        "Компонентная архитектура фронтенда организована по принципу Feature-Sliced Design: "
        "каждая функциональная область (auth, workspace, project, task, messenger, gamification, "
        "ai-assistant) выделена в отдельный slice с собственными компонентами, хуками, "
        "API-клиентами и store. Глобальное состояние управляется через React Context API "
        "для аутентификации (AuthContext) и текущего рабочего пространства (WorkspaceContext). "
        "Локальное состояние компонентов управляется через useState и useReducer."
    )
    add_normal_paragraph(
        doc,
        "Интерфейс реализует адаптивный дизайн с использованием утилитарных классов "
        "TailwindCSS, поддерживающий три брейкпоинта: mobile (<768px, одноколоночный layout, "
        "скрытый sidebar с hamburger menu), tablet (768-1024px, компактный sidebar), "
        "desktop (>1024px, полноразмерный layout). Анимации и переходы реализованы через "
        "Framer Motion 12.38.0: AnimatePresence для монтирования/размонтирования компонентов, "
        "motion.div с spring physics для плавных переходов, layout animations для анимации "
        "перегруппировки элементов Kanban-доски."
    )
    add_normal_paragraph(
        doc,
        "Интернационализация (i18next 25.10.2) поддерживает три языка: русский (ru), "
        "английский (en) и казахский (kz). Переводы организованы по namespaces (common, "
        "auth, workspace, gamification, ai) и загружаются динамически (lazy loading) "
        "через i18next-http-backend. Язык интерфейса определяется настройкой профиля "
        "(UserProfile.Locale) и может быть изменён в настройках."
    )
    add_normal_paragraph(
        doc,
        "Административная панель (Admin) предоставляет функции управления платформой: "
        "обзор системы (overview - метрики: количество пользователей, рабочих пространств, "
        "задач, активность за период), управление пользователями (users - список "
        "пользователей с фильтрацией, блокировка/разблокировка, назначение ролей), "
        "управление рабочими пространствами (workspaces - обзор всех пространств, "
        "удаление), операционные действия (ops - очистка устаревших данных, "
        "перестроение индексов) и настройка публичного лендинга (landing - "
        "редактирование контента страниц). Доступ к административной панели ограничен "
        "пользователями с IsAdmin=true."
    )
    add_normal_paragraph(
        doc,
        "UX-паттерны интерфейса включают: skeleton loaders для отображения загрузки данных "
        "(вместо пустого экрана пользователь видит анимированные заглушки), optimistic updates "
        "для операций с задачами (задача визуально перемещается мгновенно, а подтверждение "
        "с сервера приходит асинхронно), toast notifications для системных уведомлений (через "
        "react-hot-toast с поддержкой очереди), infinite scroll для списка сообщений "
        "мессенджера (cursor-based пагинация с автоматической загрузкой при прокрутке), "
        "а также debounced search для фильтрации задач и участников."
    )

    # 2.7
    doc.add_paragraph("2.7 Инфраструктура и развёртывание", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Вся инфраструктура платформы Collaby контейнеризирована с использованием Docker "
        "и оркестрирована через Kubernetes. Конфигурация kubernetes/runtime-spec.json определяет "
        "полный набор сервисов, необходимых для работы платформы."
    )

    headers_docker = ["Сервис", "Образ / Dockerfile", "Порт", "Healthcheck", "Зависимости"]
    rows_docker = [
        ["postgres", "postgres:17-alpine", "5432", "pg_isready", " -"],
        ["mongo-db", "mongo:8", "27017", "mongosh --eval", " -"],
        ["cache (Redis)", "redis:7-alpine", "6379", "redis-cli ping", " -"],
        ["events (Kafka)", "bitnami/kafka:3.9", "9092", "kafka-broker-api-versions", " -"],
        ["events-bootstrap", "Dockerfile.bootstrap", " -", " -", "events"],
        ["access", "services/AccessHost", "5001", "HTTP /health", "postgres, cache, events"],
        ["workspace", "services/WorkspaceHost", "5003", "HTTP /health", "postgres, cache, events"],
        ["realtime", "services/RealtimeHost", "5004", "HTTP /health", "mongo-db, cache, events"],
        ["engagement", "services/EngagementHost", "5006", "HTTP /health", "postgres, mongo-db, cache, events"],
        ["assistant", "services/AgentService", "8100", "HTTP /health", " -"],
        ["frontend", "Caddyfile + SPA build", "80, 443", "HTTP /", "access, workspace, realtime, engagement"],
        ["tunnel", "cloudflare/cloudflared", " -", " -", "frontend"],
    ]
    add_simple_table(doc, headers_docker, rows_docker, "Сервисы Kubernetes")

    add_normal_paragraph(
        doc,
        "Каждый сервис имеет настроенный healthcheck для обеспечения правильного порядка "
        "запуска через depends_on с условием service_healthy. Порядок запуска: (1) базы данных "
        "и кеш (postgres, mongo-db, cache, events), (2) инициализация Kafka-топиков "
        "(events-bootstrap), (3) .NET-хосты (access, workspace, realtime, engagement), "
        "(4) AI-ассистент (assistant), (5) фронтенд (frontend), (6) Cloudflare Tunnel "
        "(tunnel). Все .NET-хосты используют multi-stage Docker builds: первый этап "
        "(sdk:10.0) выполняет dotnet restore и dotnet publish, второй этап (aspnet:10.0) "
        "содержит только runtime и опубликованные артефакты, что минимизирует размер образа."
    )
    add_normal_paragraph(
        doc,
        "CI/CD реализован через GitHub CI с четырьмя этапами (stages). Этап build: сборка "
        "Docker-образов для каждого хоста с использованием BuildKit (DOCKER_BUILDKIT=1) "
        "и layer caching через GitHub Container Registry. Этап dotnet: компиляция всех "
        ".NET-проектов (dotnet build --no-restore), запуск линтера (dotnet format --verify-no-changes). "
        "Этап test: запуск юнит-тестов (dotnet test с xUnit), проверка архитектурных границ "
        "(ServiceBoundaryArchitectureTests), линтинг фронтенда (eslint). Этап deploy: "
        "автоматическое развёртывание на продуктивном сервере при push в ветку main через "
        "Ansible playbook."
    )

    _insert_pillow_diagram(doc, _generate_cicd_diagram(), "Этапы CI/CD-пайплайна GitHub CI")

    add_normal_paragraph(
        doc,
        "Развёртывание на продуктивном сервере автоматизировано с помощью Ansible. "
        "Плейбук deploy-launch.yml выполняет следующие шаги. Предварительные проверки "
        "(preflight): проверка версии Docker, наличия GPU (для assistant), доступности "
        "диска, валидации переменных окружения. Инициализация баз данных (databases): "
        "создание баз данных PostgreSQL с отдельными учётными записями для каждого сервиса, "
        "создание баз данных MongoDB с ACL, настройка Redis ACL. Сборка и запуск (rollout): "
        "git pull, docker compose build, docker compose up -d с поочерёдным запуском сервисов "
        "и проверкой здоровья каждого. Настройка Cloudflare (tunnel): запуск cloudflared tunnel "
        "с конфигурацией маршрутов."
    )
    add_normal_paragraph(
        doc,
        "Роль collaby_deploy содержит каталог сервисов (service-catalog.json), определяющий "
        "состав и порядок развёртывания. Каталог описывает для каждого сервиса: name (имя "
        "Kubernetes сервиса), type (dotnet/python/static/infrastructure), port (порт "
        "healthcheck), healthcheck_path (/health или /), depends_on (зависимости от других "
        "сервисов). Ansible использует каталог для определения порядка запуска и проверки "
        "здоровья каждого сервиса перед запуском зависящих от него."
    )
    add_normal_paragraph(
        doc,
        "Доступ к платформе осуществляется через Cloudflare Tunnel [24] с интеграцией "
        "Cloudflare Access (Zero Trust). Cloudflare Tunnel создаёт зашифрованный туннель "
        "между сервером и edge-сетью Cloudflare без необходимости открытия входящих портов "
        "на сервере. Cloudflare Access обеспечивает дополнительный уровень аутентификации "
        "на уровне CDN, что защищает от несанкционированного доступа к API."
    )
    add_normal_paragraph(
        doc,
        "Система наблюдаемости (observability) реализована на стеке Grafana [25]. Prometheus "
        "собирает метрики из .NET-хостов (через prometheus-net) и AgentService (через "
        "prometheus_client): HTTP-метрики (запросы/сек, latency, error rate), Kafka-метрики "
        "(consumer lag, messages processed), бизнес-метрики (tasks completed, XP awarded). "
        "Loki агрегирует логи из всех контейнеров через Docker log driver. Tempo обеспечивает "
        "распределённую трассировку через OpenTelemetry [26]: каждый HTTP-запрос получает trace ID, "
        "который передаётся через все сервисы для сквозного отслеживания. Grafana предоставляет "
        "единый дашборд для визуализации метрик, логов и трассировок."
    )

    # 2.8
    doc.add_paragraph("2.8 Тестирование и безопасность", style="Heading 2")
    add_normal_paragraph(
        doc,
        "Тестирование платформы осуществляется на нескольких уровнях, обеспечивая комплексную "
        "проверку функциональности, архитектурных ограничений и безопасности."
    )
    add_normal_paragraph(doc, "Юнит-тестирование:", bold=True)
    add_normal_paragraph(
        doc,
        "Юнит-тесты для .NET-сервисов написаны с использованием фреймворка xUnit 2.9 "
        "и проверяют бизнес-логику изолированных модулей. Для мокирования зависимостей "
        "используется Moq 4.20. Тесты покрывают ключевые сценарии: XpService (начисление XP "
        "с учётом приоритета и множителя streak, откат при TaskReopened, корректное обновление "
        "уровня), QuestService (обновление прогресса для разных типов событий, завершение "
        "квеста при достижении порога, сброс прогресса при смене окна), StreakService "
        "(инкремент серии, заморозка при пропуске, сброс при недостатке заморозок), "
        "ShopService (покупка с проверкой баланса, предотвращение повторной покупки "
        "нерасходуемых предметов, атомарность транзакции)."
    )
    add_normal_paragraph(
        doc,
        "Для фронтенда используется Vitest 3.2 с jsdom для эмуляции DOM. Тесты покрывают "
        "утилитарные функции (форматирование дат, вычисление прогресса уровня, валидация "
        "форм), хуки (useAuth, useWorkspace, useGamification) и компоненты (TaskCard, "
        "QuestCard, LeaderboardRow). Для тестирования компонентов используется "
        "React Testing Library с подходом testing-library/react."
    )
    add_normal_paragraph(doc, "Архитектурные тесты:", bold=True)
    add_normal_paragraph(
        doc,
        "Архитектурные тесты (ServiceBoundaryArchitectureTests) реализованы с помощью "
        "рефлексии .NET для проверки соблюдения границ ответственности между доменными "
        "модулями. Тесты загружают сборки каждого сервиса и проверяют, что: (1) типы "
        "из пространства имён IdentityService не ссылаются на типы ManagementService "
        "или GamificationService напрямую; (2) межсервисное взаимодействие осуществляется "
        "исключительно через Kafka-контракты (IntegrationEvent) и HTTP API; (3) каждый "
        "сервис использует только свой DbContext и не обращается к DbContext другого "
        "модуля. Данные тесты выполняются в CI/CD на каждый push и предотвращают "
        "случайное нарушение архитектурных границ."
    )
    add_normal_paragraph(doc, "Интеграционные тесты:", bold=True)
    add_normal_paragraph(
        doc,
        "Интеграционные тесты проверяют взаимодействие между компонентами на уровне HTTP API. "
        "Для каждого хоста используется WebApplicationFactory из Microsoft.AspNetCore.Mvc.Testing, "
        "которая запускает in-memory HTTP-сервер с реальной конфигурацией. Тесты проверяют: "
        "полный цикл регистрации и аутентификации (register -> verify email -> login -> "
        "refresh token), создание рабочего пространства с проектом и задачами, корректность "
        "маршрутизации API, валидацию входных данных (FluentValidation), обработку ошибок "
        "(404, 403, 400)."
    )

    add_normal_paragraph(doc, "Безопасность:", bold=True)
    add_normal_paragraph(
        doc,
        "Аутентификация реализована через JWT-токены с refresh-token ротацией. Access-токен "
        "имеет короткий срок жизни (15 минут) и содержит claims: sub (userId), email, "
        "username, role. Refresh-токен хранится в HttpOnly Secure cookie с SameSite=Strict "
        "и сроком жизни 7 дней. При каждом использовании refresh-токен ротируется: "
        "выпускается новый токен, а старый помечается как отозванный (RevokedAt). "
        "Это предотвращает повторное использование перехваченного refresh-токена."
    )
    add_normal_paragraph(
        doc,
        "OAuth 2.0 интеграция с Google позволяет входить через сторонний провайдер. "
        "Процесс: фронтенд получает id_token от Google, отправляет его на POST /api/auth/google, "
        "IdentityService валидирует id_token через Google.Apis.Auth (проверка подписи, "
        "audience, issuer, expiration), создаёт или связывает аккаунт через OAuthAccount "
        "и выпускает JWT-токены."
    )
    add_normal_paragraph(
        doc,
        "Двухфакторная аутентификация реализована по стандарту TOTP (RFC 6238) с использованием "
        "библиотеки Otp.NET 1.4.1. При включении 2FA генерируется 160-битный секретный ключ "
        "(SecretKey), который пользователь добавляет в Google Authenticator через QR-код. "
        "При входе пользователь вводит 6-значный TOTP-код, который валидируется с допустимым "
        "отклонением в одно временное окно (±30 секунд). Коды восстановления (RecoveryCodes) "
        "генерируются при включении 2FA и могут быть использованы однократно для входа "
        "без TOTP-кода."
    )
    add_normal_paragraph(
        doc,
        "Ограничение частоты запросов (rate limiting) реализовано на уровне ASP.NET Core "
        "middleware с хранением счётчиков в Redis. Используется алгоритм скользящего окна "
        "(sliding window): для каждого userId и endpoint ведётся sorted set в Redis с "
        "временными метками запросов. При превышении лимита (по умолчанию 100 запросов "
        "в минуту для API, 5 запросов в минуту для аутентификации) возвращается HTTP 429 "
        "Too Many Requests с заголовком Retry-After."
    )
    add_normal_paragraph(
        doc,
        "Пароли хешируются с использованием BCrypt (BCrypt.Net 4.1.0) с cost factor 12, "
        "что обеспечивает устойчивость к brute-force атакам. Каждый сервис имеет изолированные "
        "учётные записи для PostgreSQL (отдельный role с GRANT только на свою базу), "
        "MongoDB (отдельный пользователь с readWrite только на свою базу) и Redis "
        "(ACL с доступом только к определённым key patterns). Межсервисные вызовы защищены "
        "внутренними API-ключами (InternalAuth), передаваемыми в заголовке X-Internal-Auth-Key. "
        "Ключи генерируются как случайные 256-битные строки и хранятся в переменных окружения."
    )
    add_normal_paragraph(
        doc,
        "Дополнительные меры безопасности: CORS настроен с whitelist разрешённых origins, "
        "HSTS заголовки устанавливаются через Caddy, Content-Security-Policy ограничивает "
        "загрузку ресурсов, все HTTP-запросы между сервисами выполняются внутри Docker network "
        "(не доступны извне), секреты (JWT-ключи, API-ключи, строки подключения) передаются "
        "через переменные окружения и не хранятся в коде."
    )


# ---------------------------------------------------------------------------
# Conclusion
# ---------------------------------------------------------------------------

def build_conclusion(doc: Document):
    global _current_section
    _current_section = 0

    doc.add_paragraph("ЗАКЛЮЧЕНИЕ", style="Heading 1")
    add_normal_paragraph(
        doc,
        "В ходе выполнения дипломного проекта были успешно решены все поставленные задачи "
        "и достигнута основная цель - спроектирована и разработана веб-платформа Collaby "
        "для совместной работы распределённых команд с интегрированной системой геймификации "
        "и AI-ассистентом."
    )
    items = [
        ("Проведён анализ предметной области и сравнительный обзор существующих решений "
         "(Jira, Asana, Monday.com, Slack, MS Teams, Notion), подтвердивший отсутствие "
         "платформы, объединяющей управление задачами, коммуникацию, геймификацию и AI.",
         "Анализ выполнен на основе восьми функциональных критериев и трёх теоретических "
         "моделей геймификации (типология Бартла, Octalysis Framework, теория самодетерминации). "
         "Результаты анализа подтвердили, что ни одна из существующих платформ не предоставляет "
         "комплексной игровой экономики с системой опыта, квестами, магазином, сериями активности "
         "и деревьями навыков. Данный анализ обосновал целесообразность разработки Collaby."),

        ("Обоснован выбор технологий (.NET 10, React 19, PostgreSQL, MongoDB, Redis, "
         "Apache Kafka, Docker) и архитектуры SOA (MSA-ready) с консолидированными "
         "хостами и возможностью извлечения модулей.",
         "Для каждой технологии проведено сравнение с альтернативами: .NET 10 vs Java/Node.js/Go, "
         "React 19 vs Vue/Angular/Svelte, PostgreSQL vs MySQL/SQL Server. Архитектура SOA (MSA-ready) "
         "обоснована как оптимальный компромисс между монолитом и микросервисами для команды "
         "из трёх разработчиков, обеспечивающий модульность и возможность масштабирования."),

        ("Спроектирована и реализована сервис-ориентированная архитектура с пятью хостами "
         "(access, workspace, realtime, engagement, assistant), 9 Kafka-топиками и "
         "5 изолированными базами данных PostgreSQL.",
         "Архитектура включает паттерны Transactional Outbox для гарантированной доставки событий, "
         "CQRS для разделения моделей чтения и записи, Event-driven Architecture для слабой "
         "связанности сервисов и хореографическую Saga для согласованного удаления данных "
         "пользователя во всех сервисах."),

        ("Разработана комплексная система геймификации, включающая двухуровневую систему XP, "
         "ежедневные и еженедельные квесты с оконной системой назначения, магазин виртуальных "
         "предметов, таблицы лидеров с Redis-кешированием, серии активности с механикой "
         "заморозки и деревья навыков.",
         "Система геймификации научно обоснована на базе теории самодетерминации и фреймворка "
         "Octalysis. Реализован полный событийный конвейер: от завершения задачи в ManagementService "
         "через Kafka до начисления XP, обновления квестов и мгновенного уведомления пользователя "
         "через SignalR. Среднее время реакции системы составляет менее 2 секунд."),

        ("Реализована интеграция локального AI-ассистента на базе модели Qwen 2.5-1.5B "
         "с менторским режимом, ассистентом рабочего пространства и поиском по базе знаний.",
         "AI-ассистент работает локально без обращения к облачным сервисам, что обеспечивает "
         "конфиденциальность проектных данных. Реализована поддержка tool-use для интеграции "
         "с контекстом рабочего пространства, менторский режим для анализа записей встреч "
         "и генерации черновиков задач."),

        ("Разработано клиентское SPA-приложение на React 19 с адаптивным дизайном, "
         "интернационализацией (RU/EN/KZ), Kanban-доской с drag-and-drop и реальным "
         "временем через SignalR.",
         "Интерфейс поддерживает три брейкпоинта (mobile, tablet, desktop), skeleton loaders "
         "для индикации загрузки, optimistic updates для мгновенной обратной связи, анимации "
         "через Framer Motion и полную интернационализацию на трёх языках."),

        ("Выполнено развёртывание системы в контейнерной среде Docker с автоматизацией "
         "через Ansible и GitHub CI/CD, защищённым доступом через Cloudflare Zero Trust "
         "и наблюдаемостью через стек Grafana.",
         "Инфраструктура включает 12+ Docker-контейнеров с healthcheck-ами и правильным порядком "
         "запуска, CI/CD с четырьмя этапами (build, dotnet, test, deploy), Ansible playbook "
         "для автоматического развёртывания и стек наблюдаемости (Prometheus, Loki, Tempo, Grafana) "
         "для мониторинга метрик, логов и распределённых трассировок.")
    ]
    for i, (summary, detail) in enumerate(items, 1):
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = FIRST_LINE_INDENT
        run = p.add_run(f"{i}. {summary}")
        run.font.name = FONT_NAME
        run.font.size = FONT_SIZE
        add_normal_paragraph(doc, detail)

    add_normal_paragraph(
        doc,
        "Ключевым результатом работы является полнофункциональный прототип платформы Collaby, "
        "развёрнутый на продуктивном сервере и доступный через защищённый канал. Платформа "
        "успешно объединяет управление проектами, корпоративный мессенджер, видеозвонки, "
        "развитую систему геймификации и AI-ассистент в едином интерфейсе. Система прошла "
        "функциональное тестирование, архитектурную валидацию и развёртывание на продуктивном "
        "сервере, подтвердив работоспособность всех компонентов."
    )
    add_normal_paragraph(
        doc,
        "В качестве направлений дальнейшего развития определены: разработка мобильного "
        "приложения на React Native для расширения доступности платформы, реализация "
        "федеративного протокола для межсерверного взаимодействия (аналогично ActivityPub), "
        "расширение возможностей AI-ассистента с поддержкой более мощных языковых моделей "
        "(Qwen 2.5-7B, Llama 3.2-8B) и интеграцией с внешними сервисами (GitHub, GitHub), "
        "а также проведение A/B-тестирования эффективности системы геймификации на реальных "
        "командах для количественной оценки влияния на продуктивность и вовлечённость."
    )


# ---------------------------------------------------------------------------
# Appendices
# ---------------------------------------------------------------------------

def build_appendix_tz(doc: Document):
    global _current_section
    _current_section = 0

    doc.add_paragraph("ПРИЛОЖЕНИЕ А - Техническое задание", style="Heading 1")
    add_empty_line(doc)
    add_centered_text(doc, "ТЕХНИЧЕСКОЕ ЗАДАНИЕ", bold=True)
    add_centered_text(doc, "на разработку веб-платформы Collaby", bold=False, size=Pt(12))
    add_empty_line(doc)

    add_normal_paragraph(doc, "1. Общие сведения", bold=True)
    add_normal_paragraph(doc, "Наименование: «Веб-платформа для совместной работы Collaby с системой геймификации и AI-ассистентом».")
    add_normal_paragraph(doc, f"Заказчик: {UNIVERSITY_SHORT}, {DEPARTMENT}.")
    add_normal_paragraph(doc, "Исполнители: ФИО Участника 1, ФИО Участника 2, ФИО Участника 3.")
    add_normal_paragraph(doc, "Основание для разработки: дипломный проект по образовательной программе 6B06102 - Computer Science.")
    add_normal_paragraph(doc, f"Сроки выполнения: сентябрь 2025 - июнь {YEAR}.")
    add_empty_line(doc)

    add_normal_paragraph(doc, "2. Назначение и цели", bold=True)
    add_normal_paragraph(doc, "Платформа предназначена для организации совместной работы распределённых команд разработчиков программного обеспечения. Платформа должна объединить управление задачами по методологии Kanban, корпоративный мессенджер с поддержкой WebRTC-видеозвонков, систему геймификации для повышения мотивации участников и AI-ассистент для анализа проектного контекста в едином веб-интерфейсе.")
    add_normal_paragraph(doc, "Цель разработки - создание интегрированной платформы, устраняющей необходимость использования нескольких разрозненных инструментов и дополняющей стандартные функции совместной работы комплексной системой геймификации и локальным AI-ассистентом.")
    add_empty_line(doc)

    add_normal_paragraph(doc, "3. Требования к функциональности", bold=True)
    funcs = [
        "Управление рабочими пространствами: создание, редактирование, удаление рабочих пространств с ролевой моделью доступа (Owner, Admin, Member). Приглашение участников по email. Логотип и описание пространства.",
        "Управление проектами: создание проектов внутри рабочего пространства с автоматическим созданием статусных колонок Kanban (Backlog, To Do, In Progress, Review, Done). Настраиваемые колонки с возможностью добавления, удаления, переименования и изменения порядка.",
        "Kanban-доска: визуальное отображение задач в колонках с поддержкой drag-and-drop перемещения между колонками. Карточки задач с отображением приоритета, исполнителя, меток, стикеров и дедлайна. Обновление в реальном времени через SignalR.",
        "Управление задачами: создание, редактирование, удаление задач с полями title, description, priority (None/Low/Medium/High/Critical), assignee, dueDate, labels, stickerColor. Подзадачи с чекбоксами. Комментарии к задачам.",
        "Корпоративный мессенджер: прямые и групповые чаты с поддержкой реального времени (SignalR). Отслеживание присутствия (online/offline/away). Редактирование и удаление сообщений. Индикаторы печати (typing indicators).",
        "WebRTC-видеозвонки: сигнализация через SignalR (SDP offer/answer, ICE candidates). Управление сессиями звонков. Поддержка peer-to-peer модели.",
        "Система геймификации: двухуровневая система XP (глобальный и проектный), уровни с формулой прогрессии, виртуальная валюта (coins), ежедневные и еженедельные квесты, магазин виртуальных предметов (рамки, темы, значки, расходуемые предметы), таблицы лидеров (глобальный и проектный), серии активности (streaks) с механикой заморозки, деревья навыков с ветвлением.",
        "AI-ассистент на базе Qwen 2.5-1.5B: чат с учётом контекста проекта, менторский режим для анализа записей встреч, генерация черновиков задач, ассистент рабочего пространства с поиском по базе знаний.",
        "Аутентификация и безопасность: JWT с refresh-token ротацией, OAuth 2.0 (Google), двухфакторная аутентификация (TOTP), rate limiting, BCrypt для паролей.",
        "Административная панель: обзор системы, управление пользователями (блокировка, назначение ролей), управление рабочими пространствами, операционные действия.",
        "Интернационализация: поддержка русского, английского и казахского языков с динамической загрузкой переводов.",
        "Аналитика: графики активности по проектам, статистика по участникам, персональные метрики продуктивности.",
    ]
    for i, func in enumerate(funcs, 1):
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = FIRST_LINE_INDENT
        run = p.add_run(f"3.{i}. {func}")
        run.font.name = FONT_NAME
        run.font.size = FONT_SIZE
    add_empty_line(doc)

    add_normal_paragraph(doc, "4. Требования к технической реализации", bold=True)
    add_normal_paragraph(doc, "Серверная часть: .NET 10, ASP.NET Core, Entity Framework Core 10.0.5, C# 13.")
    add_normal_paragraph(doc, "Клиентская часть: React 19.2.4, Vite 8.0.1, TailwindCSS 4.2.2, TypeScript 5.8.")
    add_normal_paragraph(doc, "Базы данных: PostgreSQL 17 (5 изолированных баз), MongoDB 8 (3 базы), Redis 7 (с ACL).")
    add_normal_paragraph(doc, "Событийная шина: Apache Kafka 3.9 (9 типизированных топиков с JSON-схемами).")
    add_normal_paragraph(doc, "AI-модуль: Python 3.12, FastAPI, Qwen 2.5-1.5B (GGUF, Q4_K_M), llama-cpp-python с CUDA.")
    add_normal_paragraph(doc, "Контейнеризация: Docker, Kubernetes (12+ сервисов с healthcheck).")
    add_normal_paragraph(doc, "CI/CD: GitHub CI (4 этапа: build, dotnet, test, deploy), Ansible.")
    add_normal_paragraph(doc, "Reverse proxy: Caddy (автоматический TLS, HTTP/2, WebSocket proxy).")
    add_normal_paragraph(doc, "Наблюдаемость: Grafana, Prometheus, Loki, Tempo, OpenTelemetry.")
    add_empty_line(doc)

    add_normal_paragraph(doc, "5. Требования к развёртыванию", bold=True)
    add_normal_paragraph(doc, "Система должна разворачиваться в контейнерной среде Docker на Linux-сервере с минимальными требованиями: 4 ядра CPU, 16 ГБ RAM, 100 ГБ SSD. Для AI-модуля требуется GPU с поддержкой CUDA и минимум 4 ГБ VRAM (опционально: CPU-режим с пониженной производительностью). Доступ к системе обеспечивается через Cloudflare Tunnel с Zero Trust аутентификацией.")
    add_empty_line(doc)

    add_normal_paragraph(doc, "6. Порядок сдачи и приёмки", bold=True)
    add_normal_paragraph(doc, "Сдача проекта осуществляется поэтапно согласно графику подготовки. Приёмка включает: демонстрацию работоспособности всех функциональных модулей, проверку развёртывания на продуктивном сервере, прохождение юнит-тестов и архитектурных тестов, предоставление пояснительной записки и исходного кода.")


def build_appendix_references(doc: Document):
    global _current_section
    _current_section = 0

    doc.add_paragraph("ПРИЛОЖЕНИЕ Б - Список использованной литературы", style="Heading 1")
    add_empty_line(doc)

    refs = [
        "Microsoft. .NET 10 Documentation [Электронный ресурс]. - URL: https://learn.microsoft.com/dotnet/ (дата обращения: 15.04.2026).",
        "Microsoft. ASP.NET Core Documentation [Электронный ресурс]. - URL: https://learn.microsoft.com/aspnet/core/ (дата обращения: 15.04.2026).",
        "Microsoft. Entity Framework Core Documentation [Электронный ресурс]. - URL: https://learn.microsoft.com/ef/core/ (дата обращения: 15.04.2026).",
        "Meta. React 19 Documentation [Электронный ресурс]. - URL: https://react.dev/ (дата обращения: 15.04.2026).",
        "PostgreSQL Global Development Group. PostgreSQL 17 Documentation [Электронный ресурс]. - URL: https://www.postgresql.org/docs/ (дата обращения: 15.04.2026).",
        "MongoDB, Inc. MongoDB Manual [Электронный ресурс]. - URL: https://www.mongodb.com/docs/manual/ (дата обращения: 15.04.2026).",
        "Redis Ltd. Redis Documentation [Электронный ресурс]. - URL: https://redis.io/docs/ (дата обращения: 15.04.2026).",
        "Apache Software Foundation. Apache Kafka Documentation [Электронный ресурс]. - URL: https://kafka.apache.org/documentation/ (дата обращения: 15.04.2026).",
        "Fowler M. Patterns of Enterprise Application Architecture. - Addison-Wesley, 2002. - 560 p.",
        "Richardson C. Microservices Patterns: With Examples in Java. - Manning Publications, 2018. - 520 p.",
        "Newman S. Building Microservices: Designing Fine-Grained Systems. - O'Reilly Media, 2021. - 616 p.",
        "Deterding S. et al. From Game Design Elements to Gamefulness: Defining Gamification // Proceedings of the 15th International Academic MindTrek Conference. - 2011. - P. 9-15.",
        "Hamari J., Koivisto J., Sarsa H. Does Gamification Work? A Literature Review of Empirical Studies on Gamification // 47th Hawaii International Conference on System Sciences. - 2014. - P. 3025-3034.",
        "Morschheuser B. et al. How to Gamify? A Method for Designing Gamification // Proceedings of the 50th Hawaii International Conference on System Sciences. - 2017.",
        "Bartle R. Hearts, Clubs, Diamonds, Spades: Players Who Suit MUDs // Journal of MUD Research. - 1996. - Vol. 1, No. 1.",
        "Chou Y. Actionable Gamification: Beyond Points, Badges, and Leaderboards. - Octalysis Media, 2015. - 500 p.",
        "Deci E.L., Ryan R.M. Self-Determination Theory and the Facilitation of Intrinsic Motivation, Social Development, and Well-Being // American Psychologist. - 2000. - Vol. 55, No. 1. - P. 68-78.",
        "W3C. WebRTC 1.0: Real-Time Communication Between Browsers [Электронный ресурс]. - URL: https://www.w3.org/TR/webrtc/ (дата обращения: 15.04.2026).",
        "Alibaba Cloud. Qwen 2.5 Technical Report [Электронный ресурс]. - URL: https://qwenlm.github.io/ (дата обращения: 15.04.2026).",
        "Vite. Vite Documentation [Электронный ресурс]. - URL: https://vite.dev/ (дата обращения: 15.04.2026).",
        "TailwindCSS. Tailwind CSS Documentation [Электронный ресурс]. - URL: https://tailwindcss.com/docs/ (дата обращения: 15.04.2026).",
        "ГОСТ 34.602-2020. Информационные технологии. Техническое задание на создание автоматизированной системы.",
        "ГОСТ 7.32-2017. Система стандартов по информации, библиотечному и издательскому делу. Отчёт о научно-исследовательской работе. Структура и правила оформления.",
        "Cloudflare. Cloudflare Zero Trust Documentation [Электронный ресурс]. - URL: https://developers.cloudflare.com/cloudflare-one/ (дата обращения: 15.04.2026).",
        "Grafana Labs. Grafana Documentation [Электронный ресурс]. - URL: https://grafana.com/docs/ (дата обращения: 15.04.2026).",
        "OpenTelemetry. OpenTelemetry Documentation [Электронный ресурс]. - URL: https://opentelemetry.io/docs/ (дата обращения: 15.04.2026).",
        "Docker, Inc. Docker Documentation [Электронный ресурс]. - URL: https://docs.docker.com/ (дата обращения: 15.04.2026).",
        "Microsoft. SignalR Documentation [Электронный ресурс]. - URL: https://learn.microsoft.com/aspnet/core/signalr/ (дата обращения: 15.04.2026).",
    ]

    for i, ref in enumerate(refs, 1):
        p = doc.add_paragraph()
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(2)
        run = p.add_run(f"{i}. {ref}")
        run.font.name = FONT_NAME
        run.font.size = Pt(12)


def build_appendix_code(doc: Document):
    global _current_section
    _current_section = 0

    doc.add_paragraph("ПРИЛОЖЕНИЕ В - Ключевые листинги кода", style="Heading 1")
    add_empty_line(doc)

    add_normal_paragraph(
        doc,
        "В данном приложении представлены ключевые фрагменты исходного кода, "
        "иллюстрирующие архитектурные решения и реализацию основных модулей "
        "платформы Collaby. Полный исходный код доступен в репозитории проекта.",
        indent=False
    )
    add_empty_line(doc)

    # Key listings: path, caption, max_lines
    listings = [
        {
            "path": "services/GamificationService/Domain/Entities/UserGlobalXp.cs",
            "caption": "Листинг В.1 - Сущность UserGlobalXp",
            "max_lines": 60
        },
        {
            "path": "services/GamificationService/Domain/Entities/XpTransaction.cs",
            "caption": "Листинг В.2 - Сущность XpTransaction",
            "max_lines": 60
        },
        {
            "path": "services/GamificationService/Domain/Entities/Quest.cs",
            "caption": "Листинг В.3 - Сущность Quest",
            "max_lines": 60
        },
        {
            "path": "services/GamificationService/Domain/Entities/ShopItem.cs",
            "caption": "Листинг В.4 - Сущность ShopItem",
            "max_lines": 60
        },
        {
            "path": "services/GamificationService/Domain/Entities/SkillTreeNode.cs",
            "caption": "Листинг В.5 - Сущность SkillTreeNode",
            "max_lines": 50
        },
        {
            "path": "services/GamificationService/Domain/Enums/XpReason.cs",
            "caption": "Листинг В.6 - Перечисление XpReason",
            "max_lines": 40
        },
        {
            "path": "services/GamificationService/Application/Services/XpService.cs",
            "caption": "Листинг В.7 - XpService: начисление опыта",
            "max_lines": 80
        },
        {
            "path": "services/GamificationService/Application/Services/StreakService.cs",
            "caption": "Листинг В.8 - StreakService: серии активности",
            "max_lines": 70
        },
        {
            "path": "services/GamificationService/Application/Services/QuestService.cs",
            "caption": "Листинг В.9 - QuestService: система квестов",
            "max_lines": 70
        },
        {
            "path": "services/GamificationService/Application/Services/ShopService.cs",
            "caption": "Листинг В.10 - ShopService: виртуальный магазин",
            "max_lines": 70
        },
        {
            "path": "services/GamificationService/Hubs/GamificationHub.cs",
            "caption": "Листинг В.11 - GamificationHub: SignalR-уведомления",
            "max_lines": 50
        },
        {
            "path": "services/IdentityService/Domain/Entities/User.cs",
            "caption": "Листинг В.12 - Сущность User (IdentityService)",
            "max_lines": 60
        },
        {
            "path": "services/ManagementService/Domain/Entities/Task.cs",
            "caption": "Листинг В.13 - Сущность TaskItem (ManagementService)",
            "max_lines": 60
        },
        {
            "path": "services/ManagementService/Hubs/ManagementHub.cs",
            "caption": "Листинг В.14 - ManagementHub: обновления Kanban-доски",
            "max_lines": 60
        },
        {
            "path": "platform/contracts/events/topic-registry.json",
            "caption": "Листинг В.15 - Реестр Kafka-топиков (фрагмент)",
            "max_lines": 60
        },
    ]

    for listing in listings:
        filepath = PROJECT_ROOT / listing["path"]
        max_lines = listing.get("max_lines", 60)
        if filepath.exists():
            try:
                code = filepath.read_text(encoding="utf-8")
                lines = code.split("\n")
                if len(lines) > max_lines:
                    code = "\n".join(lines[:max_lines]) + "\n// ... (далее опущено)"
                add_code_block(doc, code, listing["caption"])
                add_empty_line(doc)
            except Exception as e:
                add_normal_paragraph(doc, f'[Ошибка чтения файла {listing["path"]}: {e}]', indent=False)
        else:
            add_normal_paragraph(doc, f'[Файл {listing["path"]} не найден]', indent=False)


# ---------------------------------------------------------------------------
# Page numbering
# ---------------------------------------------------------------------------

def add_page_numbers(doc: Document):
    """Add centered page numbers starting from the second section (Introduction onwards).
    Pre-pages (title, task, annotations, TOC) have no visible page numbers per GOST."""
    sections = list(doc.sections)
    for i, section in enumerate(sections):
        footer = section.footer
        footer.is_linked_to_previous = False
        p = footer.paragraphs[0] if footer.paragraphs else footer.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)

        if i == 0:
            # First section (pre-pages): no page numbers
            continue

        run = p.add_run()
        fldChar1 = parse_xml(f'<w:fldChar {nsdecls("w")} w:fldCharType="begin"/>')
        run._r.append(fldChar1)

        run2 = p.add_run()
        instrText = parse_xml(f'<w:instrText {nsdecls("w")} xml:space="preserve"> PAGE </w:instrText>')
        run2._r.append(instrText)

        run3 = p.add_run()
        fldChar2 = parse_xml(f'<w:fldChar {nsdecls("w")} w:fldCharType="end"/>')
        run3._r.append(fldChar2)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    print("Генерация дипломной записки Collaby...")
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    doc = create_document()

    # Pre-pages
    print(" [1/14] Титульный лист 1...")
    build_title_page_1(doc)

    print(" [2/14] Титульный лист 2 (допуск к защите)...")
    build_title_page_2(doc)

    print(" [3/14] Задание на ДП...")
    build_task_assignment(doc)

    print(" [4/14] График подготовки...")
    build_schedule(doc)

    print(" [5/14] Аннотация (RU)...")
    build_annotation_ru(doc)

    print(" [6/14] Аннотация (KZ)...")
    build_annotation_kz(doc)

    print(" [7/14] Аннотация (EN)...")
    build_annotation_en(doc)

    print(" [8/14] Содержание (TOC)...")
    add_toc(doc)

    # Main content
    print(" [9/14] Введение...")
    build_introduction(doc)

    print(" [10/14] Раздел 1: Исследовательско-технологический...")
    build_section_1(doc)

    print(" [11/14] Раздел 2: Проектно-экспериментальный...")
    build_section_2(doc)

    print(" [12/14] Заключение...")
    build_conclusion(doc)

    # Appendices
    print(" [13/14] Приложения А, Б...")
    build_appendix_tz(doc)
    build_appendix_references(doc)

    print(" [14/14] Приложение В: Листинги кода...")
    build_appendix_code(doc)

    # Page numbers
    add_page_numbers(doc)

    # Save
    doc.save(str(OUTPUT_FILE))
    print(f"\nГотово! Файл сохранён: {OUTPUT_FILE}")
    print(f"Размер: {OUTPUT_FILE.stat().st_size / 1024:.1f} КБ")

    # Character count estimation
    total_chars = 0
    for p in doc.paragraphs:
        total_chars += len(p.text)
    for t in doc.tables:
        for row in t.rows:
            for cell in row.cells:
                total_chars += len(cell.text)
    print(f"Примерное количество символов: {total_chars:,}")
    print(f"Примерное количество страниц: {total_chars // 3500}")


if __name__ == "__main__":
    main()
