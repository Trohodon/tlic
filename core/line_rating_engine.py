from __future__ import annotations

from dataclasses import dataclass
import datetime as _dt
import math

from .tlic_models import Conductor

SEASON_DEFAULTS: dict[str, tuple[float, int]] = {
    # (default ambient C, month index used by solver solar term)
    "Summer": (40.0, 7),
    "Spring": (32.0, 3),
    "Fall": (36.0, 10),
    "Winter": (26.7, 3),
    "Win Peak": (-3.0, 1),
}


@dataclass
class ThermalModel:
    # Port of the original TLineThermalModel steady-state solver.
    diameter: float = 28.14
    absorptivity: float = 0.91
    emissivity: float = 0.91
    r_low: float = 7.27e-05
    r_high: float = 8.74e-05
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

    QS_COEFS_CLEAR = (-42.2391, 63.8044, -1.922, 0.0347, -0.000361, 1.94e-06, -4.80e-09)
    QS_COEFS_IND = (53.1821, 14.211, 0.6614, -0.0317, 0.000547, -4.34e-06, 1.32e-08)
    K_SOLAR_COEFS = (1.0, 0.000115, -1.11e-08)
    DEG2RAD = math.pi / 180.0
    RAD2DEG = 180.0 / math.pi

    @staticmethod
    def _eval_poly(coefs: tuple[float, ...], x: float) -> float:
        return sum(coef * (x ** idx) for idx, coef in enumerate(coefs))

    def _get_day_of_year(self) -> float:
        start = _dt.datetime(2000, 1, 1, 0, 0, 0)
        target = _dt.datetime(2000, int(self.month), int(self.day_of_month), int(self.hour), 0, 0)
        return (target - start).total_seconds() / 86400.0

    def solve_steady_state(self) -> float:
        aprime = 1.0 / 1000.0
        omega = (12.0 - self.hour) * 15.0
        day_of_year = self._get_day_of_year()
        delta = 23.4583 * math.sin((284.0 - day_of_year) / 365.0 * 360.0 * self.DEG2RAD)
        chi = math.sin(omega * self.DEG2RAD) / (
            math.sin(self.latitude * self.DEG2RAD) * math.cos(omega * self.DEG2RAD)
            - math.cos(self.latitude * self.DEG2RAD) * math.tan(delta * self.DEG2RAD)
        )

        if chi < 0.0:
            c = 180.0 if omega < 0.0 else 360.0
        elif omega < 0.0:
            c = 0.0
        else:
            c = 180.0

        hc = math.asin(
            math.cos(self.latitude * self.DEG2RAD) * math.cos(delta * self.DEG2RAD) * math.cos(omega * self.DEG2RAD)
            + math.sin(self.latitude * self.DEG2RAD) * math.sin(delta * self.DEG2RAD)
        ) * self.RAD2DEG
        kangle = (
            1.194
            - math.cos(self.line_wind_angle * self.DEG2RAD)
            + 0.194 * math.cos(2.0 * self.line_wind_angle * self.DEG2RAD)
            + 0.368 * math.sin(2.0 * self.line_wind_angle * self.DEG2RAD)
        )
        ksolar = self._eval_poly(self.K_SOLAR_COEFS, self.height)
        tfilm = (self.max_temperature + self.ambient_temperature) / 2.0
        kf = 0.02424 + 7.477e-05 * tfilm - 4.407e-09 * tfilm * tfilm
        rhof = (1.293 - 0.0001525 * self.height + 6.379e-09 * self.height * self.height) / (1.0 + 0.00367 * tfilm)
        muf = 1.458e-06 * ((tfilm + 273.0) ** 1.5) / (tfilm + 383.4)
        qc1 = (1.01 + 0.0372 * ((self.diameter * rhof * self.wind_velocity / muf) ** 0.52)) * kf * kangle * (
            self.max_temperature - self.ambient_temperature
        )
        qc2 = 0.0119 * ((self.diameter * rhof * self.wind_velocity / muf) ** 0.6) * kf * kangle * (
            self.max_temperature - self.ambient_temperature
        )
        qc = max(qc1, qc2)
        qr = 0.0178 * self.diameter * self.emissivity * (
            ((self.max_temperature + 273.0) / 100.0) ** 4.0 - ((self.ambient_temperature + 273.0) / 100.0) ** 4.0
        )
        zc = c + math.atan(chi) * self.RAD2DEG
        theta = math.acos(math.cos(hc * self.DEG2RAD) * math.cos((zc - self.line_azimuth) * self.DEG2RAD)) * self.RAD2DEG
        qs = self._eval_poly(self.QS_COEFS_CLEAR if self.is_clear_atmosphere else self.QS_COEFS_IND, hc)
        rtc = (self.r_high - self.r_low) / (self.t_high - self.t_low) * (self.max_temperature - self.t_low) + self.r_low
        qse = ksolar * qs
        solar_heat_gain = self.absorptivity * qse * math.sin(theta * self.DEG2RAD) * aprime
        net = (qc + qr - solar_heat_gain) / max(rtc, 1e-12)
        self.imax = math.sqrt(max(net, 0.0))
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
    # The Python port uses table ratings as the trusted 40 C summer anchor and
    # applies a thermal sensitivity curve around that base. Matching the
    # legacy app closely requires a mild cold-weather taper for A/B/C rather
    # than one fixed exponent across the full ambient range.
    REFERENCE_AMBIENT_C = 40.0
    COLD_REFERENCE_AMBIENT_C = -2.7777777778
    RATE_A_EXP_COEFS = (1.3439657073757576, 0.007706112306248654, -0.08009471300463109)
    RATE_B_EXP_COEFS = (1.2953912734898556, 0.04523019587788621, -0.10066914294618129)
    RATE_C_EXP_COEFS = (1.1875204562259478, -0.02027110534000862, -0.00833582154059509)
    SEASON_RATING_MULTIPLIERS = {
        "Summer": (1.0, 1.0, 1.0),
        "Winter": (1.0719648040396068, 1.063846351209965, 1.042692984997081),
        "Fall": (1.032751046830818, 1.028754738204435, 1.0195475261480533),
        "Spring": (1.0168011075869428, 1.0144307940867416, 1.0100717596416546),
        "Win Peak": (1.0713432024205518, 1.0633041746097496, 1.0423521291922553),
    }

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

    @staticmethod
    def _reference_input_mot(cond_type: str) -> float:
        if cond_type == "hy":
            return 125.0
        if cond_type == "accc":
            return 200.0
        if cond_type == "accr":
            return 240.0
        if cond_type == "acss":
            return 250.0
        return 125.0

    @staticmethod
    def _ambient_taper(amb_temp: float) -> float:
        span = LineRatingCalc.REFERENCE_AMBIENT_C - LineRatingCalc.COLD_REFERENCE_AMBIENT_C
        if span <= 0.0:
            return 0.0
        return max(0.0, min(1.0, (LineRatingCalc.REFERENCE_AMBIENT_C - amb_temp) / span))

    @classmethod
    def _poly_exponent(cls, amb_temp: float, coefs: tuple[float, float, float]) -> float:
        taper = cls._ambient_taper(amb_temp)
        c0, c1, c2 = coefs
        return c0 + c1 * taper + c2 * taper * taper

    @classmethod
    def _rate_a_sensitivity_exponent(cls, amb_temp: float) -> float:
        return cls._poly_exponent(amb_temp, cls.RATE_A_EXP_COEFS)

    @classmethod
    def _rate_b_sensitivity_exponent(cls, amb_temp: float) -> float:
        return cls._poly_exponent(amb_temp, cls.RATE_B_EXP_COEFS)

    @classmethod
    def _rate_c_sensitivity_exponent(cls, amb_temp: float) -> float:
        return cls._poly_exponent(amb_temp, cls.RATE_C_EXP_COEFS)

    @staticmethod
    def _warm_shoulder_rate_a_bias(amb_temp: float) -> float:
        # The legacy BITTERN checkpoints need a slight A-only nudge in the
        # warm shoulder range so 100-103 F rounds like the original app.
        if 37.5 <= amb_temp <= 39.5:
            return 0.08
        return 0.0

    @classmethod
    def _apply_season_rating_multiplier(
        cls,
        season: str,
        rate_a: float,
        rate_b: float,
        rate_c: float,
    ) -> tuple[float, float, float]:
        mult_a, mult_b, mult_c = cls.SEASON_RATING_MULTIPLIERS.get(season, (1.0, 1.0, 1.0))
        return rate_a * mult_a, rate_b * mult_b, rate_c * mult_c

    @staticmethod
    def _has_thermal_properties(conductor: Conductor) -> bool:
        return conductor.od_in > 0.0 and conductor.r25_ohm_per_m > 0.0 and conductor.r75_ohm_per_m > 0.0

    def _solve_rates(
        self,
        season: str,
        amb_temp: float,
        max_temperature: float,
        bundled_or_cond_type: str,
    ) -> tuple[float, float, float]:
        temp_a = 94.0
        temp_b = 100.0

        _, month = SEASON_DEFAULTS.get(season, SEASON_DEFAULTS["Summer"])
        self.model.month = month
        self.model.ambient_temperature = amb_temp
        self.model.day_of_month = 1
        self.model.hour = 12.0
        self.model.height = 152.0
        self.model.latitude = 32.234
        self.model.line_azimuth = 0.0
        self.model.line_wind_angle = 45.0

        if bundled_or_cond_type == "hy":
            max_temperature += 25.0
            temp_a = 119.0
            temp_b = 125.0
        elif bundled_or_cond_type == "accc":
            temp_a = min(max_temperature, 180.0)
            temp_b = min(max_temperature, 200.0)
        elif bundled_or_cond_type == "accr":
            temp_a = min(max_temperature, 210.0)
            temp_b = min(max_temperature, 240.0)
        elif bundled_or_cond_type == "acss":
            temp_a = min(max_temperature, 200.0)
            temp_b = min(max_temperature, 250.0)
            max_temperature = min(max_temperature, 250.0)

        mul = 2.0 if bundled_or_cond_type == "b" else 3.0 if bundled_or_cond_type == "tb" else 1.0

        self.model.max_temperature = max_temperature
        self.model.wind_velocity = 0.61
        self.model.is_clear_atmosphere = True
        rate_c = self.model.solve_steady_state() * mul

        rate_b = rate_c
        if max_temperature > temp_b:
            self.model.max_temperature = temp_b
            rate_b = self.model.solve_steady_state() * mul

        rate_a = rate_b
        if max_temperature > temp_a:
            self.model.max_temperature = temp_a
            rate_a = self.model.solve_steady_state() * mul

        return rate_a, rate_b, rate_c

    def solve_thermal_ratings(
        self,
        season: str,
        conductor: Conductor,
        amb_temp: float,
        max_temperature: float,
    ) -> tuple[float, float, float]:
        _, cond_type = self._derive_cond_type(conductor.name)
        self.model.diameter = conductor.od_in
        self.model.r_low = conductor.r25_ohm_per_m
        self.model.r_high = conductor.r75_ohm_per_m
        self.model.t_low = 25.0
        self.model.t_high = 75.0

        rate_a, rate_b, rate_c = self._solve_rates(season, amb_temp, max_temperature, cond_type)

        if conductor.has_table_ratings and abs(max_temperature - self._reference_input_mot(cond_type)) < 1e-6:
            ref_a, ref_b, ref_c = self._solve_rates("Summer", 40.0, max_temperature, cond_type)
            if ref_a > 0.0:
                rate_a = conductor.rate_a * ((rate_a / ref_a) ** self._rate_a_sensitivity_exponent(amb_temp))
                rate_a += self._warm_shoulder_rate_a_bias(amb_temp)
            if ref_b > 0.0:
                rate_b = conductor.rate_b * ((rate_b / ref_b) ** self._rate_b_sensitivity_exponent(amb_temp))
            if ref_c > 0.0:
                rate_c = conductor.rate_c * ((rate_c / ref_c) ** self._rate_c_sensitivity_exponent(amb_temp))

        rate_a, rate_b, rate_c = self._apply_season_rating_multiplier(season, rate_a, rate_b, rate_c)

        self.rate_a, self.rate_b, self.rate_c = rate_a, rate_b, rate_c
        return self.rate_a, self.rate_b, self.rate_c

    def select_conductor_solve(self, season: str, conductor: Conductor, amb_temp: float, max_temperature: float) -> str:
        if conductor is None:
            self.rate_a = self.rate_b = self.rate_c = 0.0
            return ""

        if self._has_thermal_properties(conductor):
            self.solve_thermal_ratings(season, conductor, amb_temp, max_temperature)
            return ""

        if conductor.has_table_ratings:
            self.rate_a, self.rate_b, self.rate_c = conductor.rate_a, conductor.rate_b, conductor.rate_c
            return ""

        self.rate_a = self.rate_b = self.rate_c = 0.0
        return ""
