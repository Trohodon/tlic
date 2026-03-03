from __future__ import annotations

import csv
import os
import re
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


def _canonical_header(value: str | None) -> str:
    txt = str(value or "").strip().replace("\ufeff", "").lower()
    txt = re.sub(r"\([^)]*\)", "", txt)
    return re.sub(r"[^a-z0-9]+", "", txt)


def _find_column_indexes(headers: list[str], *aliases: str) -> list[int]:
    wanted = {_canonical_header(alias) for alias in aliases if alias}
    return [idx for idx, header in enumerate(headers) if _canonical_header(header) in wanted]


def _get_cell(row: list[str], indexes: list[int], default: str = "") -> str:
    for idx in indexes:
        if 0 <= idx < len(row):
            value = row[idx].strip()
            if value != "":
                return value
    return default


def _get_last_cell(row: list[str], indexes: list[int], default: str = "") -> str:
    for idx in reversed(indexes):
        if 0 <= idx < len(row):
            value = row[idx].strip()
            if value != "":
                return value
    return default


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
        reader = csv.reader(f, delimiter=delim)
        rows = list(reader)
        if not rows:
            return sample_conductors(), sample_statics()

        headers = rows[0]
        static_idxs = _find_column_indexes(headers, "is_static", "static", "wire_type")
        display_name_idxs = _find_column_indexes(headers, "display_name", "name", "conductor", "condname", "description")
        code_idxs = _find_column_indexes(headers, "code_name", "code")
        type_idxs = _find_column_indexes(headers, "type")
        size_idxs = _find_column_indexes(headers, "size")
        r_idxs = _find_column_indexes(headers, "r", "r_ohm_per_mi", "resistance", "r60", "r(ohms/mi)")
        xl_idxs = _find_column_indexes(headers, "xl", "x", "xl_ohm_per_mi", "xl(ohms/mi)")
        xc_idxs = _find_column_indexes(headers, "xc", "c", "xc_mohm_mi", "xc(ohms/mi)")
        gmr_idxs = _find_column_indexes(headers, "gmr", "gmr_ft", "gmr(ft)")
        rad_idxs = _find_column_indexes(headers, "radius", "rad", "radius_ft", "radius(ft)")
        rate_a_idxs = _find_column_indexes(headers, "ratea", "rate_a", "ampa", "ratea(a)")
        rate_b_idxs = _find_column_indexes(headers, "rateb", "rate_b", "ampb", "rateb(a)")
        rate_c_idxs = _find_column_indexes(headers, "ratec", "rate_c", "ampc", "ratec(a)")
        od_idxs = _find_column_indexes(headers, "od_in", "diameter", "od")
        lbs_outer_idxs = _find_column_indexes(headers, "lbs_kft_outer")
        lbs_inner_idxs = _find_column_indexes(headers, "lbs_kft_inner")
        r25_idxs = _find_column_indexes(headers, "r25")
        r75_idxs = _find_column_indexes(headers, "r75")

        for raw in rows[1:]:
            if not raw:
                continue
            row = [str(value).strip() for value in raw]

            # Shared conductor sheets can contain duplicate NAME columns. For
            # the Dominion file layout, the last "Name" column is the real
            # conductor/static name and the earlier "NAME" column contains an
            # internal label like "2-Jan" that should not drive the UI.
            name = _get_last_cell(row, display_name_idxs)
            alt_name = _get_cell(row, display_name_idxs)
            if not name:
                code = _get_cell(row, code_idxs)
                ctype = _get_cell(row, type_idxs)
                size = _get_cell(row, size_idxs)
                if size and size != "0":
                    name = f"{size} {ctype} ({code})".strip()
                else:
                    name = f"{code} ({ctype})".strip()
            if not name:
                continue

            aliases: list[str] = []
            if alt_name and alt_name.lower() != name.lower():
                aliases.append(alt_name)

            is_static = str(_get_cell(row, static_idxs, "0")).lower() in {
                "1",
                "true",
                "static",
            }

            r = _num(_get_cell(row, r_idxs), 0.08)
            xl = _num(_get_cell(row, xl_idxs), 0.35)
            xc = _num(_get_cell(row, xc_idxs), 0.20)
            gmr = _num(_get_cell(row, gmr_idxs), 0.02)
            rad = _num(_get_cell(row, rad_idxs), 0.04)

            rate_a = _num(_get_cell(row, rate_a_idxs), 600.0)
            rate_b = _num(_get_cell(row, rate_b_idxs), 700.0)
            rate_c = _num(_get_cell(row, rate_c_idxs), 800.0)

            od_in = _num(_get_cell(row, od_idxs), 1.0)
            lbs_outer = _num(_get_cell(row, lbs_outer_idxs), 0.0)
            lbs_inner = _num(_get_cell(row, lbs_inner_idxs), 0.0)
            r25 = _num(_get_cell(row, r25_idxs), 0.00005)
            r75 = _num(_get_cell(row, r75_idxs), r25 * 1.202)
            if r75 > 999.9:
                r75 = r25 * 1.202

            # Unit conversion copied from original intent.
            od_mm = od_in * 25.4
            r25_m = r25 / 1609.344
            r75_m = r75 / 1609.344
            ctype = _get_cell(row, type_idxs).upper()
            if ctype == "CU":
                cp = lbs_outer / 1000.0 * 192.0
            elif ctype == "ACCC":
                cp = lbs_outer / 1000.0 * 433.0 + lbs_inner / 1000.0 * 369.0
            else:
                cp = lbs_outer / 1000.0 * 433.0 + lbs_inner / 1000.0 * 216.0
            heat_cap = cp * 3.28084

            conductor = Conductor(
                name=name,
                aliases=aliases,
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
        if isinstance(item, Conductor):
            for alias in item.aliases:
                if alias.strip().lower() == low:
                    return item
    for item in items:
        if low in item.name.strip().lower():
            return item
        if isinstance(item, Conductor):
            for alias in item.aliases:
                if low in alias.strip().lower():
                    return item
    return None


def sample_conductors() -> list[Conductor]:
    return [
        Conductor(
            "1272 ACSR (BITTERN)",
            r_ohm_per_mi=0.0812,
            xl_ohm_per_mi=0.378,
            xc_mohm_mi=0.0855,
            rate_a=2267,
            rate_b=2632,
            rate_c=2632,
            gmr_ft=0.0448,
            radius_ft=0.05604,
            od_in=1.6,
            r25_ohm_per_m=1.4e-5,
            r75_ohm_per_m=1.9e-5,
            heat_cap_ws_per_m_c=900,
        ),
        Conductor("1033.5 ACCC (ACCC)", 
            r_ohm_per_mi=0.028, 
            xl_ohm_per_mi=0.28, 
            xc_mohm_mi=0.20, 
            rate_a=1500, 
            rate_b=1650, 
            rate_c=1750, 
            gmr_ft=0.039, 
            radius_ft=0.051, 
            od_in=1.45, 
            r25_ohm_per_m=1.8e-5, 
            r75_ohm_per_m=2.2e-5, 
            heat_cap_ws_per_m_c=870
        ),
        Conductor("1113 ACCR (ACCR)", 
            r_ohm_per_mi=0.026, 
            xl_ohm_per_mi=0.27, 
            xc_mohm_mi=0.20, 
            rate_a=1650, 
            rate_b=1800, 
            rate_c=1900, 
            gmr_ft=0.04, 
            radius_ft=0.052, 
            od_in=1.5, 
            r25_ohm_per_m=1.6e-5, 
            r75_ohm_per_m=2.1e-5, 
            heat_cap_ws_per_m_c=880
        ),
        Conductor("1113 ACSS (ACSS)", 
            r_ohm_per_mi=0.024, 
            xl_ohm_per_mi=0.27, 
            xc_mohm_mi=0.20, 
            rate_a=1500, 
            rate_b=1700, 
            rate_c=1800, 
            gmr_ft=0.04, 
            radius_ft=0.052, 
            od_in=1.5, 
            r25_ohm_per_m=1.5e-5, 
            r75_ohm_per_m=2.0e-5, 
            heat_cap_ws_per_m_c=890
        ),
    ]


def sample_statics() -> list[Conductor]:
    return [
        Conductor(
            "1/4 GALV (None)",
            is_static_default=True,
            r_ohm_per_mi=7.83,
            xl_ohm_per_mi=2.07,
            xc_mohm_mi=0.1244,
            rate_a=130,
            rate_b=130,
            rate_c=130,
            gmr_ft=0.0104,
            radius_ft=0.0103,
        ),
        Conductor("7#8 AW (none)", 
            is_static_default=True, 
            r_ohm_per_mi=3.06, 
            xl_ohm_per_mi=0.749, 
            xc_mohm_mi=0.1226, 
            rate_a=190, 
            rate_b=190, 
            rate_c=190, 
            gmr_ft=0.0021, 
            radius_ft=0.016),
    ]


def sample_structures() -> list[Structure]:
    return [
        Structure("BPV",
            a=[Point(-18, 50), 
               Point(0, 58), 
               Point(18, 50)], 
            g=[Point(0, 72), 
               Point(0, 0)]
        ),
        Structure("HFRAME", 
            a=[Point(-22, 45), 
               Point(0, 45), 
               Point(22, 45)], 
            g=[Point(0, 62), 
               Point(0, 0)]
        ),
    ]
