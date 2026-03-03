from __future__ import annotations

from dataclasses import dataclass

from .tlic_models import Conductor

SEASON_DEFAULTS: dict[str, tuple[float, int]] = {
    "Summer": (40.0, 7),
    "Spring": (32.0, 3),
    "Fall": (36.0, 10),
    "Winter": (26.7, 1),
    "Win Peak": (-3.0, 1),
}


@dataclass
class ThermalModel:
    diameter: float = 25.4
    absorptivity: float = 0.91
    emissivity: float = 0.91
    r_low: float = 1e-5
    r_high: float = 2e-5
    t_low: float = 25.0
    t_high: float = 75.0
    month: int = 7
    ambient_temperature: float = 40.0
    day_of_month: int = 1
    hour: float = 12.0
    height: float = 152.0
    latitude: float = 32.234
    line_azimuth: float = 0.0
    line_wind_angle: float = 45.0
    max_temperature: float = 125.0
    wind_velocity: float = 0.61
    is_clear_atmosphere: bool = True
    imax: float = 0.0

    def solve_steady_state(self) -> float:
        # Engineering approximation replacing unavailable proprietary TLineCalc model.
        temp_rise = max(self.max_temperature - self.ambient_temperature, 1.0)
        r_eff = max(self.r_low + (self.r_high - self.r_low) * max(self.max_temperature - self.t_low, 0.0) / max(self.t_high - self.t_low, 1.0), 1e-8)
        cooling = max((self.wind_velocity + 0.4) * (self.emissivity + 0.3), 0.05)
        solar = 1.0 + (0.06 if self.month in (5, 6, 7, 8, 9) else 0.0) * self.absorptivity
        dia = max(self.diameter / 25.4, 0.2)
        self.imax = ((temp_rise * dia * cooling) / (r_eff * solar)) ** 0.5 * 0.0075
        return self.imax

    def temperature_at(self, current: float, tol: float = 0.1) -> float:
        guess = self.ambient_temperature + 10.0
        for _ in range(40):
            self.max_temperature = guess
            imax = self.solve_steady_state()
            err = imax - current
            if abs(err) < max(tol, 0.01):
                break
            guess += (-err) * 6.0
            if guess < self.ambient_temperature + 1.0:
                guess = self.ambient_temperature + 1.0
            if guess > 350:
                guess = 350
        return guess


class LineRatingCalc:
    def __init__(self) -> None:
        self.rate_a = 0.0
        self.rate_b = 0.0
        self.rate_c = 0.0
        self.model = ThermalModel()

    def _derive_cond_type(self, cond_name: str) -> tuple[str, str]:
        cond = cond_name.strip().lower()
        cond_type = ""
        if cond.startswith("tb"):
            cond_type = "tb"
            cond = cond[2:]
        elif cond.startswith("b"):
            cond_type = "b"
            cond = cond[1:]
        elif cond.startswith("hy"):
            cond_type = "hy"
            cond = cond[2:]
        elif "accc" in cond:
            cond_type = "accc"
        elif "accr" in cond:
            cond_type = "accr"
        elif "acss" in cond:
            cond_type = "acss"
        return cond, cond_type

    def select_conductor_solve(self, season: str, conductor: Conductor, amb_temp: float, max_temperature: float) -> str:
        _, cond_type = self._derive_cond_type(conductor.name)
        self.model.diameter = conductor.od_in
        self.model.r_low = conductor.r25_ohm_per_m
        self.model.r_high = conductor.r75_ohm_per_m
        self.model.t_low = 25.0
        self.model.t_high = 75.0
        self.solve_steady_state_current(season, amb_temp, max_temperature, cond_type)
        return ""

    def solve_steady_state_current(self, season: str, amb_temp: float, max_temperature: float, bundled_or_cond_type: str) -> None:
        temp_a = 94.0
        temp_b = 100.0

        _, month = SEASON_DEFAULTS.get(season, SEASON_DEFAULTS["Summer"])
        self.model.month = month
        self.model.ambient_temperature = amb_temp
        self.model.day_of_month = 1
        self.model.hour = 12
        self.model.height = 152
        self.model.latitude = 32.234
        self.model.line_azimuth = 0
        self.model.line_wind_angle = 45

        if bundled_or_cond_type == "hy":
            max_temperature += 25
            temp_a = 119
            temp_b = 125
        elif bundled_or_cond_type == "accc":
            temp_a = min(max_temperature, 180)
            temp_b = min(max_temperature, 200)
        elif bundled_or_cond_type == "accr":
            temp_a = min(max_temperature, 210)
            temp_b = min(max_temperature, 240)
        elif bundled_or_cond_type == "acss":
            # ACSS UPDATE: explicit A/B/C caps required by project spec.
            temp_a = min(max_temperature, 200)
            temp_b = min(max_temperature, 250)
            max_temperature = min(max_temperature, 250)

        self.model.max_temperature = max_temperature
        self.model.wind_velocity = 0.61
        self.model.is_clear_atmosphere = True
        self.model.solve_steady_state()

        mul = 1.0
        if bundled_or_cond_type == "b":
            mul = 2.0
        elif bundled_or_cond_type == "tb":
            mul = 3.0

        self.rate_c = self.model.imax * mul

        self.rate_b = self.rate_c
        if max_temperature > temp_b:
            self.model.max_temperature = temp_b
            self.model.solve_steady_state()
            self.rate_b = self.model.imax * mul

        self.rate_a = self.rate_b
        if max_temperature > temp_a:
            self.model.max_temperature = temp_a
            self.model.solve_steady_state()
            self.rate_a = self.model.imax * mul
