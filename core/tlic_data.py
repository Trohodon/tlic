from __future__ import annotations

import csv
import os
from typing import Iterable

from .tlic_models import Conductor, Point, Structure


def _num(value: str | None, default: float = 0.0) -> float:
    if value is None:
        return default
    txt = str(value).strip().replace("\ufeff", "")
    if txt == "":
        return default
    try:
        return float(txt)
    except ValueError:
        return default


def _normalize(row: dict[str, str]) -> dict[str, str]:
    return {k.strip().lower(): (v.strip() if isinstance(v, str) else v) for k, v in row.items()}


def _sniff_delimiter(path: str) -> str:
    with open(path, "r", newline="", encoding="utf-8-sig", errors="ignore") as f:
        sample = f.read(4096)
    if "\t" in sample and sample.count("\t") > sample.count(","):
        return "\t"
    return ","


def load_conductors(path: str) -> tuple[list[Conductor], list[Conductor]]:
    if not os.path.exists(path):
        return sample_conductors(), sample_statics()

    delim = _sniff_delimiter(path)
    phase: list[Conductor] = []
    statics: list[Conductor] = []

    with open(path, "r", newline="", encoding="utf-8-sig", errors="ignore") as f:
        reader = csv.DictReader(f, delimiter=delim)
        if reader.fieldnames is None:
            return sample_conductors(), sample_statics()

        for raw in reader:
            row = _normalize(raw)

            name = (
                row.get("display_name")
                or row.get("name")
                or row.get("conductor")
                or row.get("condname")
                or row.get("description")
            )
            if not name:
                code = row.get("code_name") or row.get("code") or ""
                ctype = row.get("type") or ""
                size = row.get("size") or ""
                if size and size != "0":
                    name = f"{size} {ctype} ({code})".strip()
                else:
                    name = f"{code} ({ctype})".strip()

            is_static = str(row.get("is_static") or row.get("static") or row.get("wire_type") or "0").lower() in {
                "1",
                "true",
                "static",
            }

            r = _num(row.get("r") or row.get("r_ohm_per_mi") or row.get("resistance") or row.get("r60"), 0.08)
            xl = _num(row.get("xl") or row.get("x") or row.get("xl_ohm_per_mi"), 0.35)
            xc = _num(row.get("xc") or row.get("c") or row.get("xc_mohm_mi"), 0.20)
            gmr = _num(row.get("gmr") or row.get("gmr_ft"), 0.02)
            rad = _num(row.get("radius") or row.get("rad") or row.get("radius_ft"), 0.04)

            rate_a = _num(row.get("ratea") or row.get("rate_a") or row.get("ampa"), 600.0)
            rate_b = _num(row.get("rateb") or row.get("rate_b") or row.get("ampb"), 700.0)
            rate_c = _num(row.get("ratec") or row.get("rate_c") or row.get("ampc"), 800.0)

            od_in = _num(row.get("od_in") or row.get("diameter") or row.get("od"), 1.0)
            lbs_outer = _num(row.get("lbs_kft_outer"), 0.0)
            lbs_inner = _num(row.get("lbs_kft_inner"), 0.0)
            r25 = _num(row.get("r25"), 0.00005)
            r75 = _num(row.get("r75"), r25 * 1.202)
            if r75 > 999.9:
                r75 = r25 * 1.202

            # Unit conversion copied from original intent.
            od_mm = od_in * 25.4
            r25_m = r25 / 1609.344
            r75_m = r75 / 1609.344
            ctype = (row.get("type") or "").upper()
            if ctype == "CU":
                cp = lbs_outer / 1000.0 * 192.0
            elif ctype == "ACCC":
                cp = lbs_outer / 1000.0 * 433.0 + lbs_inner / 1000.0 * 369.0
            else:
                cp = lbs_outer / 1000.0 * 433.0 + lbs_inner / 1000.0 * 216.0
            heat_cap = cp * 3.28084

            conductor = Conductor(
                name=name,
                is_static_default=is_static,
                gmr_ft=gmr,
                radius_ft=rad,
                r_ohm_per_mi=r,
                xl_ohm_per_mi=xl,
                xc_mohm_mi=xc,
                rate_a=rate_a,
                rate_b=rate_b,
                rate_c=rate_c,
                od_in=od_mm,
                r25_ohm_per_m=r25_m,
                r75_ohm_per_m=r75_m,
                heat_cap_ws_per_m_c=heat_cap,
            )

            if is_static:
                statics.append(conductor)
            else:
                phase.append(conductor)

    if not phase:
        phase = sample_conductors()
    if not statics:
        statics = sample_statics()

    return phase, statics


def load_structures(path: str) -> list[Structure]:
    if not os.path.exists(path):
        return sample_structures()

    delim = _sniff_delimiter(path)
    structs: list[Structure] = []

    with open(path, "r", newline="", encoding="utf-8-sig", errors="ignore") as f:
        reader = csv.reader(f, delimiter=delim)
        rows = list(reader)

    if len(rows) <= 1:
        return sample_structures()

    for row in rows[1:]:
        vals = [c.strip() for c in row if c is not None and str(c).strip() != ""]
        if len(vals) < 7:
            continue

        name = vals[0]
        x1, y1, x2, y2, x3, y3 = map(_num, vals[1:7])
        g1x = _num(vals[7], 0.0) if len(vals) > 7 else 0.0
        g1y = _num(vals[8], 0.0) if len(vals) > 8 else 0.0
        g2x = _num(vals[9], 0.0) if len(vals) > 9 else 0.0
        g2y = _num(vals[10], 0.0) if len(vals) > 10 else 0.0

        structs.append(
            Structure(
                name=name,
                a=[Point(x1, y1), Point(x2, y2), Point(x3, y3)],
                g=[Point(g1x, g1y), Point(g2x, g2y)],
            )
        )

    return structs or sample_structures()


def by_name(items: Iterable[Conductor | Structure], name: str):
    low = name.strip().lower()
    for item in items:
        if item.name.strip().lower() == low:
            return item
    for item in items:
        if low in item.name.strip().lower():
            return item
    return None


def sample_conductors() -> list[Conductor]:
    return [
        Conductor("1272 ACSR (BITTERN)", r_ohm_per_mi=0.022, xl_ohm_per_mi=0.27, xc_mohm_mi=0.19, rate_a=1300, rate_b=1480, rate_c=1600, gmr_ft=0.042, radius_ft=0.055, od_in=1.6, r25_ohm_per_m=1.4e-5, r75_ohm_per_m=1.9e-5, heat_cap_ws_per_m_c=900),
        Conductor("1033.5 ACCC (ACCC)", r_ohm_per_mi=0.028, xl_ohm_per_mi=0.28, xc_mohm_mi=0.20, rate_a=1500, rate_b=1650, rate_c=1750, gmr_ft=0.039, radius_ft=0.051, od_in=1.45, r25_ohm_per_m=1.8e-5, r75_ohm_per_m=2.2e-5, heat_cap_ws_per_m_c=870),
        Conductor("1113 ACCR (ACCR)", r_ohm_per_mi=0.026, xl_ohm_per_mi=0.27, xc_mohm_mi=0.20, rate_a=1650, rate_b=1800, rate_c=1900, gmr_ft=0.04, radius_ft=0.052, od_in=1.5, r25_ohm_per_m=1.6e-5, r75_ohm_per_m=2.1e-5, heat_cap_ws_per_m_c=880),
        Conductor("1113 ACSS (ACSS)", r_ohm_per_mi=0.024, xl_ohm_per_mi=0.27, xc_mohm_mi=0.20, rate_a=1500, rate_b=1700, rate_c=1800, gmr_ft=0.04, radius_ft=0.052, od_in=1.5, r25_ohm_per_m=1.5e-5, r75_ohm_per_m=2.0e-5, heat_cap_ws_per_m_c=890),
    ]


def sample_statics() -> list[Conductor]:
    return [
        Conductor("1/4 GALV (None)", is_static_default=True, r_ohm_per_mi=0.9, xl_ohm_per_mi=0.5, xc_mohm_mi=0.1, rate_a=120, rate_b=150, rate_c=180, gmr_ft=0.01, radius_ft=0.01),
        Conductor("7#8 Alumoweld", is_static_default=True, r_ohm_per_mi=0.65, xl_ohm_per_mi=0.45, xc_mohm_mi=0.12, rate_a=220, rate_b=250, rate_c=300, gmr_ft=0.012, radius_ft=0.015),
    ]


def sample_structures() -> list[Structure]:
    return [
        Structure("BPV", a=[Point(-18, 50), Point(0, 58), Point(18, 50)], g=[Point(0, 72), Point(0, 0)]),
        Structure("HFRAME", a=[Point(-22, 45), Point(0, 45), Point(22, 45)], g=[Point(0, 62), Point(0, 0)]),
    ]
