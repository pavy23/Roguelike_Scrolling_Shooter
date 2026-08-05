#!/usr/bin/env python3
"""게임 개발 기획 문서(GDD) PDF 생성.

사람 요청 2026-08-05: "게임 개발 기획 문서 형태로 정리해서 pdf 파일을 뽑아줘"

수치는 **저장소에서 직접 읽는다.** 손으로 적어 두면 다음에 뽑을 때 조용히
낡는다 — 커밋 수·기간·코드 줄 수·테스트 수는 전부 실행 시점에 계산한다.

실행: python Tools/Docs/make_gdd_pdf.py
출력: docs/RSS_GameDesignDocument.pdf
"""
import datetime
import pathlib
import re
import subprocess

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    Image, PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle)

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "RSS_GameDesignDocument.pdf"

# 한글 글리프가 있는 폰트여야 한다 — 기본 Helvetica는 전부 네모로 찍힌다.
pdfmetrics.registerFont(TTFont("KR", "C:/Windows/Fonts/malgun.ttf"))
pdfmetrics.registerFont(TTFont("KR-Bold", "C:/Windows/Fonts/malgunbd.ttf"))


def sh(*args):
    return subprocess.run(
        args, cwd=ROOT, capture_output=True, text=True,
        encoding="utf-8", errors="ignore").stdout


def count_lines(pattern):
    total = 0
    for path in ROOT.glob(pattern):
        if path.is_file():
            total += sum(1 for _ in path.open(encoding="utf-8", errors="ignore"))
    return total


def facts():
    """문서에 박히는 수치를 저장소에서 계산한다."""
    dates = [line.strip() for line in sh(
        "git", "log", "--format=%ad", "--date=iso").splitlines() if line.strip()]
    stamps = sorted(datetime.datetime.fromisoformat(d) for d in dates)
    gap = datetime.timedelta(minutes=30)
    active = datetime.timedelta()
    previous = None
    for stamp in stamps:
        if previous is not None and stamp - previous <= gap:
            active += stamp - previous
        previous = stamp

    authors = {}
    for line in sh("git", "log", "--format=%b").splitlines():
        if line.lower().startswith("co-authored-by"):
            name = line.split(":", 1)[1].split("<")[0].strip()
            authors[name] = authors.get(name, 0) + 1

    body = sh("git", "log", "--format=%b")
    return {
        "first": stamps[0],
        "last": stamps[-1],
        "days": (stamps[-1] - stamps[0]).total_seconds() / 86400,
        "hours": active.total_seconds() / 3600,
        "commits": len(stamps),
        "authors": sorted(authors.items(), key=lambda kv: -kv[1]),
        "human_notes": len(re.findall(r"사람 (지시|보고|지적|승인)", body)),
        "reqs": len(set(re.findall(r"REQ-\d+", body))),
        "core": count_lines("Assets/Scripts/Core/**/*.cs"),
        "presentation": count_lines("Assets/Scripts/Presentation/**/*.cs"),
        "tests": count_lines("Assets/Tests/**/*.cs"),
        "data": count_lines("GameData/*.json"),
        "sprites": len(list((ROOT / "Assets/Art/Sprites").glob("*.png"))),
        "test_files": len(list((ROOT / "Assets/Tests/EditMode").glob("*.cs"))),
    }


BODY = ParagraphStyle(
    "body", fontName="KR", fontSize=9.5, leading=15,
    spaceAfter=5, textColor=colors.HexColor("#1c2430"))
H1 = ParagraphStyle(
    "h1", fontName="KR-Bold", fontSize=16, leading=21,
    spaceBefore=14, spaceAfter=7, textColor=colors.HexColor("#12325c"))
H2 = ParagraphStyle(
    "h2", fontName="KR-Bold", fontSize=11.5, leading=17,
    spaceBefore=10, spaceAfter=4, textColor=colors.HexColor("#1c4a80"))
NOTE = ParagraphStyle(
    "note", parent=BODY, fontSize=8.5, leading=13,
    textColor=colors.HexColor("#5b6675"))
TITLE = ParagraphStyle(
    "title", fontName="KR-Bold", fontSize=25, leading=31,
    alignment=TA_CENTER, textColor=colors.HexColor("#0d2440"))
SUB = ParagraphStyle(
    "sub", fontName="KR", fontSize=11, leading=17,
    alignment=TA_CENTER, textColor=colors.HexColor("#5b6675"))


def table(rows, widths, header=True):
    style = [
        ("FONTNAME", (0, 0), (-1, -1), "KR"),
        ("FONTSIZE", (0, 0), (-1, -1), 8.6),
        ("LEADING", (0, 0), (-1, -1), 12.5),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#c8d2de")),
    ]
    if header:
        style += [
            ("FONTNAME", (0, 0), (-1, 0), "KR-Bold"),
            ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e8eef6")),
            ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#12325c")),
        ]
    wrapped = [[Paragraph(str(c), BODY) for c in row] for row in rows]
    t = Table(wrapped, colWidths=widths, repeatRows=1 if header else 0)
    t.setStyle(TableStyle(style))
    return t


def build():
    f = facts()
    doc = SimpleDocTemplate(
        str(OUT), pagesize=A4,
        leftMargin=20 * mm, rightMargin=20 * mm,
        topMargin=18 * mm, bottomMargin=18 * mm,
        title="Roguelike Scrolling Shooter - 게임 개발 기획 문서",
        author="pavy23")
    s = []
    W = doc.width

    # ── 표지 ────────────────────────────────────────────────────────────
    s += [Spacer(1, 34 * mm),
          Paragraph("ROGUELIKE SCROLLING SHOOTER", TITLE),
          Spacer(1, 4 * mm),
          Paragraph("게임 개발 기획 문서 (Game Design Document)", SUB),
          Spacer(1, 2 * mm),
          Paragraph(
              f"{f['first']:%Y-%m-%d} – {f['last']:%Y-%m-%d} &middot; "
              f"커밋 {f['commits']}건", SUB),
          Spacer(1, 10 * mm)]
    # 표지는 JPEG 축소본을 쓴다 — 원본 PNG를 그대로 넣으면 PDF가 1MB를 넘어
    # 메일 첨부가 무거워진다. 문서용 해상도로 충분하다.
    shot = ROOT / "out/gdd_cover.jpg"
    if shot.exists():
        s.append(Image(str(shot), width=W, height=W * 9 / 16))
    s += [Spacer(1, 8 * mm),
          Paragraph(
              "https://pavy23.github.io/rss-play", SUB),
          PageBreak()]

    # ── 1. 개요 ─────────────────────────────────────────────────────────
    s += [Paragraph("1. 개요", H1), Paragraph(
        "그라디우스 계열 2D 횡스크롤 슈팅에 로그라이크 런 구조를 얹은 "
        "SNES풍 픽셀 아트 게임. 한 번의 런은 5개 바이옴을 관통하고, 보스를 잡을 "
        "때마다 다음 항로와 그 조건(리스크 계약)을 고른다. 죽으면 끝이며 점수는 "
        "크레딧이 되어 기체를 해금한다.", BODY)]
    s += [Paragraph("설계 원칙", H2), table([
        ["원칙", "내용"],
        ["결정론", "같은 시드 + 같은 입력 = 같은 결과. 정수 서브유닛(256/유닛) "
                   "고정소수점, 부동소수 상태 금지, System.Random 금지. "
                   "데일리 런·리플레이·이어하기가 전부 여기서 나온다."],
        ["3분리", "Simulation(순수 C#) / Presentation(Unity) / Content(JSON). "
                  "Presentation은 Core 상태를 <b>그리기만</b> 한다 — "
                  "MonoBehaviour.Update에서 게임플레이를 결정하지 않는다."],
        ["색이 곧 문법", "내 탄=주황, 적 탄=마젠타, 청록=지금 못 깎는다. "
                        "탄막이 두꺼워져도 색 하나로 갈린다."],
        ["화면이 말한다", "피격은 붉은 플래시, 근접 예고는 노란 점멸, "
                         "무적은 피격 표시 없음. 상태를 글자로 설명하지 않는다."],
    ], [30 * mm, W - 30 * mm])]

    # ── 2. 게임 구조 ────────────────────────────────────────────────────
    s += [Paragraph("2. 게임 구조", H1),
          Paragraph("2.1 런 흐름", H2),
          Paragraph(
              "5개 바이옴(고철 폐선장 → 생체 군체 → 요새 → 성운 폭풍 → 적의 코어). "
              "각 스테이지는 전반 → 중간보스 → 후반 → 스테이지 보스로 흐른다. "
              "히든 조건 3개 중 2개를 채우면 극한 항로 <b>미지의 구역</b>이 열린다.",
              BODY),
          Paragraph("2.2 파워업 — 그라디우스 게이지", H2),
          table([
              ["칸", "효과"],
              ["SPEED", "이동속도 (레벨당 +1.5)"],
              ["SHOT", "주무기 최대 6레벨 — 레벨당 데미지 +50%"],
              ["MISSILE", "미사일 + 계열 교체 (직진/확산/랜스/투하/유도)"],
              ["기체 무기", "기체 고유 무기, 재발동마다 3단 진화"],
              ["OPTION", "분신 최대 6기, 편성 3종 (궤적/고정/궤도)"],
              ["SHIELD", "실드 재고 +1 — 레벨이 아니라 장수. 0에서 맞으면 즉사"],
          ], [26 * mm, W - 26 * mm]),
          Paragraph("2.3 기체 3종", H2),
          table([
              ["기체", "성향", "고유 무기 진화", "해금"],
              ["Starter", "밸런스 (실드 1)", "더블 → 테일 가드 → 크로스 파이어", "기본"],
              ["Interceptor", "빠르고 취약 (실드 0)", "트리플 → 펄스 팬 → 버너",
               "50,000 cr"],
              ["Bulwark", "느린 탱커 (실드 2)", "레이저 → 랜스 → 프리즘 빔",
               "100,000 cr"],
          ], [24 * mm, 32 * mm, W - 24 * mm - 32 * mm - 24 * mm, 24 * mm])]

    # ── 3. 보스 설계 ────────────────────────────────────────────────────
    s += [PageBreak(), Paragraph("3. 보스 설계", H1),
          Paragraph(
              "스테이지가 오를수록 <b>구조가 한 축씩</b> 늘어난다. "
              "탄막 어휘(대구경·파편·기뢰·레이저)는 공유하되 스테이지별 "
              "시그니처를 얹는다.", BODY),
          table([
              ["보스", "구조"],
              ["St1", "이동 사다리 — 페이즈마다 이동 패턴이 바뀐다"],
              ["St2 생체", "부위 절단 — 실드는 두 다리를 부숴야 벗겨지고, "
                          "다리는 무릎 아래가 잘려 나간다"],
              ["St3 전함", "화면을 가로지르는 함체 1척. 함미 → 함체(포탑 6문) "
                          "→ 함수 코어의 3막. 함체는 스크롤 속도로 걸어 들어오고 "
                          "막마다 위아래로 이동한다. 부순 포탑 수가 코어전 "
                          "개막 밀도를 정한다"],
              ["St4 번개룡", "세그먼트 체인 소환 — 머리만 노려라"],
              ["St5 최종", "1스테이지의 내 입력이 고스트로 재생돼 함께 싸운다"],
              ["히든 (2종)", "4페이즈: 일반탄 → 관통 빔 / 부챗살 레이저 → "
                            "상하 이동 + 조밀 탄막 + 근접 예고 공격 → "
                            "몸을 버리고 작은 코어만 남는 최종 페이즈"],
          ], [24 * mm, W - 24 * mm]),
          Paragraph("3.1 예고의 원칙", H2),
          Paragraph(
              "피할 방법이 없는 공격은 패턴이 아니라 처형이다. 근접 공격은 "
              "돌진 전 1초간 <b>그 자리에 멈춰</b> 노랗게 점멸하고, 접촉 판정은 "
              "예고가 아니라 돌진 구간에만 산다. 적 유도탄은 2초 뒤 유도를 멈추고 "
              "직진한다 — 끝까지 따라오면 화면 어디로 도망쳐도 소용이 없다.",
              BODY)]

    # ── 4. 아키텍처 ─────────────────────────────────────────────────────
    s += [Paragraph("4. 기술 구조", H1), table([
        ["계층", "책임", "규모"],
        ["Simulation (Shmup.Core)",
         "순수 C#. 탄 위치·데미지·드롭 판정·보스 상태기계. Unity 참조 없음 — "
         "dotnet test로 Unity 없이 검증한다",
         f"{f['core']:,}줄"],
        ["Presentation (Unity)",
         "씬·프리팹·풀링·입력·오디오·UI. Core 상태를 그리기만 한다",
         f"{f['presentation']:,}줄"],
        ["Content (JSON)",
         "적·무기·웨이브·보상·함선·점수 데이터. 코드 배포 없이 밸런스를 바꾼다",
         f"{f['data']:,}줄"],
        ["Tests",
         f"EditMode {f['test_files']}파일 — 결정론 감사, 보스 도달성, "
         "밸런스 관계 검증",
         f"{f['tests']:,}줄"],
    ], [40 * mm, W - 40 * mm - 22 * mm, 22 * mm]),
        Paragraph("4.1 검증 체계", H2),
        Paragraph(
            "테스트는 <b>수치를 베끼지 않고 관계를 검사한다.</b> "
            "\"전함 2막 탄막이 초당 8발\"이 아니라 \"페이즈가 오를수록 밀도가 "
            "올라가고 회피 가능한 상한 안에 있다\"를 본다 — 밸런스를 손볼 때마다 "
            "관계없는 테스트가 깨지면 게이트가 지키려던 의도가 흐려진다.", BODY),
        Paragraph(
            "화면 검증은 headless 하네스(puppeteer + 픽셀 어서션)가 맡는다. "
            "HP바 감소량, 화면에 뜬 적탄 수, 콘솔 에러를 수치로 뽑아 "
            "PASS/FAIL로 떨어뜨린다.", BODY)]

    # ── 5. 개발 체계 ────────────────────────────────────────────────────
    s += [PageBreak(), Paragraph("5. 개발 체계 — 다중 AI 협업", H1),
          Paragraph(
              "사람 1명이 방향을 정하고, 역할이 다른 AI 에이전트들이 "
              "<b>소유 영역을 나눠</b> 작업했다. 자기 영역 밖의 파일은 고치지 않고 "
              "Reviews 폴더로 요청을 남긴다 — 같은 파일을 둘이 만지면 병합에서 "
              "꼬이기 때문이다.", BODY),
          table([
              ["에이전트", "역할", "소유 영역"],
              ["사람", "방향·최종 결정", "밸런스 판단, 우선순위, 공유 규칙 문서"],
              ["CLAUDE", "RENDERER + 아트/오디오",
               "Assets/ (Core 제외), 씬·프리팹·입력·오디오·UI, ProjectSettings, "
               "아트 생성 파이프라인"],
              ["CODEX", "SIMULATION",
               "Assets/Scripts/Core/ (Shmup.Core), EditMode 테스트"],
              ["GROK", "CONTENT / BALANCER",
               "GameData/*.json, 밸런스 시뮬레이터"],
              ["GEMINI", "QA / VERIFIER",
               "테스트 플랜·캡처·베이스라인·리포트 (코드 소유 없음)"],
              ["PLAYTESTER", "실플레이 체감 검증",
               "브라우저 실주행 리포트 (코드 소유 없음)"],
          ], [24 * mm, 32 * mm, W - 24 * mm - 32 * mm]),
          Paragraph("5.1 대행 규칙", H2),
          Paragraph(
              "한 에이전트가 한도에 걸리면 다른 에이전트가 대행하되, "
              "<b>대행 사실을 커밋에 명시</b>한다. 리뷰 없는 코드가 조용히 "
              "쌓이지 않게 하기 위해서다.", BODY)]

    # ── 6. 투입 실적 ────────────────────────────────────────────────────
    s += [Paragraph("6. 투입 실적", H1),
          Paragraph(
              "아래 수치는 이 문서를 뽑는 시점의 저장소에서 <b>직접 계산</b>한 "
              "것이다.", NOTE),
          table([
              ["항목", "값"],
              ["개발 기간", f"{f['first']:%Y-%m-%d} – {f['last']:%Y-%m-%d} "
                           f"({f['days']:.1f}일)"],
              ["커밋", f"{f['commits']}건"],
              ["활동 시간 (추정)",
               f"약 {f['hours']:.0f}시간 — 커밋 간격이 30분 이내인 구간만 합산"],
              ["사람 지시가 남은 커밋", f"{f['human_notes']}건"],
              ["추적된 요구사항", f"REQ {f['reqs']}종"],
              ["코드", f"Core {f['core']:,} + Presentation {f['presentation']:,} "
                      f"+ Tests {f['tests']:,}줄"],
              ["데이터", f"JSON {f['data']:,}줄"],
              ["아트", f"스프라이트 {f['sprites']}장"],
          ], [42 * mm, W - 42 * mm]),
          Paragraph("6.1 에이전트별 커밋 기여", H2),
          table(
              [["에이전트 (Co-Authored-By)", "커밋"]]
              + [[name, str(n)] for name, n in f["authors"]],
              [W - 24 * mm, 24 * mm]),
          Paragraph(
              "합계가 전체 커밋 수보다 적은 것은 초기 커밋에 co-author 표기가 "
              "없었기 때문이다.", NOTE)]

    # ── 7. 비용 ─────────────────────────────────────────────────────────
    s += [Paragraph("7. 비용", H1),
          Paragraph(
              "<b>정확한 금액은 이 문서에서 계산할 수 없다.</b> 저장소에는 토큰 "
              "사용량이나 청구 기록이 남지 않는다 — 실제 금액은 각 제공자 콘솔에서 "
              "확인해야 한다.", BODY),
          table([
              ["항목", "성격", "확인처"],
              ["Claude (Opus/Sonnet/Fable)", "구독 또는 API 종량",
               "Anthropic Console / 구독 청구서"],
              ["Codex (GPT 계열)", "구독 CLI", "OpenAI 계정"],
              ["Grok", "구독 CLI", "xAI 계정"],
              ["Gemini", "구독/무료 티어", "Google 계정"],
              ["이미지 생성 (PixelLab, gpt-image)", "종량 (장당 과금)",
               "각 서비스 대시보드"],
              ["호스팅", "GitHub Pages + Cloudflare Workers 무료 티어",
               "요금 없음 (현 트래픽 기준)"],
          ], [44 * mm, 46 * mm, W - 90 * mm]),
          Paragraph(
              "비용을 좌우한 것은 모델 요금보다 <b>재작업</b>이었다. "
              "예: 빌드에 들어가는 GameData 사본이 갱신되지 않아 기능이 통째로 "
              "빠진 채 배포된 일 — Core 테스트는 전부 통과하고 콘솔 에러도 0이라 "
              "사람이 두 번 지적할 때까지 아무도 몰랐다. 이후 빌드가 사본을 직접 "
              "맞추도록 고쳐 같은 부류를 차단했다.", BODY)]

    # ── 8. 남은 일 ──────────────────────────────────────────────────────
    s += [Paragraph("8. 남은 일", H1), table([
        ["항목", "메모"],
        ["브루드마더 레이저 3패턴 순환", "지금은 항상 같은 방향"],
        ["근접 공격 전용 애니메이션", "현재는 본체 돌진 + 파츠 점멸"],
        ["스코어보드 초기화", "워커에 관리자 엔드포인트 없음 — "
                             "KV 직접 삭제 또는 엔드포인트 추가 필요"],
        ["PC/모바일 프레이밍 통일", "코드 경로는 동일(레터박스). "
                                  "재현 화면 확보 후 판단"],
    ], [46 * mm, W - 46 * mm])]

    s += [Spacer(1, 8 * mm), Paragraph(
        f"생성 {datetime.datetime.now():%Y-%m-%d %H:%M} &middot; "
        "수치는 저장소에서 자동 계산됨 (Tools/Docs/make_gdd_pdf.py)", NOTE)]

    doc.build(s)
    print(f"saved {OUT}")


if __name__ == "__main__":
    build()
