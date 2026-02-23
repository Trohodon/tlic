from __future__ import annotations

from dataclasses import dataclass

from .line_rating_engine import LineRatingCalc
from .tlic_data import by_name
from .tlic_models import BranchOptions, BranchResult, Conductor, LineSection, Structure


@dataclass
class BranchEngine:
    rating_calc: LineRatingCalc

    def calculate(
        self,
        options: BranchOptions,
        sections: list[LineSection],
        conductors: list[Conductor],
        statics: list[Conductor],
        structures: list[Structure],
        is_summer: bool,
    ) -> BranchResult:
        result = BranchResult()
        if not sections:
            return result

        zbase = max((options.kv * options.kv) / max(options.mva_base, 0.001), 1e-6)
        bbase = 1.0 / zbase

        total_r1 = total_x1 = total_b1 = 0.0
        total_miles = 0.0

        min_rate_a = min_rate_b = min_rate_c = 0.0

        for sec in sections:
            ph = by_name(conductors + statics, sec.cond_name)
            st = by_name(statics + conductors, sec.static_name)
            _str = by_name(structures, sec.struct_name)
            if ph is None:
                continue
            if st is None:
                st = ph

            miles = max(sec.mileage, 0.0)
            total_miles += miles

            es_adj = 1.0
            if _str is not None:
                es_adj = max(_str.es / 20.0, 0.4)

            r1_mi = ph.r_ohm_per_mi
            x1_mi = ph.xl_ohm_per_mi * (1.0 + 0.02 * (es_adj - 1.0))
            b1_mi = 0.0
            if ph.xc_mohm_mi > 1e-9:
                b1_mi = 1.0 / ph.xc_mohm_mi

            total_r1 += r1_mi * miles
            total_x1 += x1_mi * miles
            total_b1 += b1_mi * miles

            self.rating_calc.select_conductor_solve(is_summer, ph, options.temp_c, sec.mot)
            ra = self.rating_calc.rate_a
            rb = self.rating_calc.rate_b
            rc = self.rating_calc.rate_c

            if min_rate_a == 0.0 or ra < min_rate_a:
                min_rate_a = ra
            if min_rate_b == 0.0 or rb < min_rate_b:
                min_rate_b = rb
            if min_rate_c == 0.0 or rc < min_rate_c:
                min_rate_c = rc

        if total_miles <= 0:
            return result

        result.length_mi = total_miles

        result.r1_pu = total_r1 / zbase
        result.x1_pu = total_x1 / zbase
        result.b1_pu = total_b1 / bbase * 1e-6

        # Sequence approximation for parity-style output where the source library is unavailable.
        result.r0_pu = result.r1_pu * 3.0
        result.x0_pu = result.x1_pu * 3.0
        result.b0_pu = result.b1_pu * 0.7

        if len(sections) == 1:
            result.z1_per_mile_r = total_r1 / total_miles
            result.z1_per_mile_x = total_x1 / total_miles
            result.y1_per_mile_b = total_b1 / total_miles
            result.z0_per_mile_r = result.z1_per_mile_r * 3.0
            result.z0_per_mile_x = result.z1_per_mile_x * 3.0
            result.y0_per_mile_b = result.y1_per_mile_b * 0.7

        result.current_rate_a = min_rate_a
        result.current_rate_b = min_rate_b
        result.current_rate_c = min_rate_c

        status = 1 if options.in_service else 0
        result.raw_string = (
            f"{options.bus1}, {options.bus2}, '{options.ckt}', {status}, "
            f"{result.r1_pu:.6f}, {result.x1_pu:.6f}, {result.b1_pu:.6f}, "
            f"{result.mva_rating_a(options.kv):.2f}, {result.mva_rating_b(options.kv):.2f}, {result.mva_rating_c(options.kv):.2f}, {result.length_mi:.3f}"
        )
        result.seq_string = (
            f"{options.bus1}, {options.bus2}, '{options.ckt}', "
            f"{result.r0_pu:.6f}, {result.x0_pu:.6f}, {result.b0_pu:.6f}"
        )

        return result
