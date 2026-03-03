"""Line rating panel migrated from LineRate_MainForm.cs."""

from __future__ import annotations

import tkinter as tk
from tkinter import ttk

from core.line_rating_engine import LineRatingCalc, SEASON_DEFAULTS
from core.tlic_data import sample_conductors


class LineRateMainForm(ttk.Frame):
    def __init__(self, parent) -> None:
        super().__init__(parent, padding=10)
        self.columnconfigure(0, weight=1)
        self.rating = LineRatingCalc()
        self.cond_list = sample_conductors()
        self.seasons = list(SEASON_DEFAULTS.keys())

        ttk.Label(self, text="Line Rating", font=("Segoe UI", 11, "bold")).grid(row=0, column=0, sticky="w")

        form = ttk.Frame(self)
        form.grid(row=1, column=0, sticky="ew", pady=(8, 8))
        form.columnconfigure(1, weight=1)

        self.var_cond = tk.StringVar(value=self.cond_list[0].name)
        self.var_season = tk.StringVar(value="Summer")
        self.var_amb = tk.StringVar(value="40")
        self.var_mot = tk.StringVar(value="125")

        ttk.Label(form, text="Conductor").grid(row=0, column=0, sticky="w")
        ttk.Combobox(form, textvariable=self.var_cond, values=[c.name for c in self.cond_list], state="readonly").grid(
            row=0, column=1, sticky="ew", padx=6
        )
        ttk.Label(form, text="Season").grid(row=1, column=0, sticky="w")
        season_box = ttk.Combobox(form, textvariable=self.var_season, values=self.seasons, state="readonly", width=12)
        season_box.grid(row=1, column=1, sticky="w", padx=6)
        season_box.bind("<<ComboboxSelected>>", lambda _e: self.on_season_change())
        ttk.Label(form, text="Ambient (C)").grid(row=2, column=0, sticky="w")
        ttk.Entry(form, textvariable=self.var_amb, width=10).grid(row=2, column=1, sticky="w", padx=6)
        ttk.Label(form, text="Max Temp MOT (C)").grid(row=3, column=0, sticky="w")
        ttk.Entry(form, textvariable=self.var_mot, width=10).grid(row=3, column=1, sticky="w", padx=6)

        ttk.Button(form, text="Calculate", command=self.calculate).grid(row=4, column=1, sticky="w", padx=6, pady=(6, 0))

        self.output = tk.Text(self, height=12, wrap="word")
        self.output.grid(row=2, column=0, sticky="nsew")

        self.calculate()

    def on_season_change(self) -> None:
        self.var_amb.set(f"{SEASON_DEFAULTS.get(self.var_season.get(), SEASON_DEFAULTS['Summer'])[0]:.1f}")
        self.calculate()

    def calculate(self) -> None:
        self.output.delete("1.0", "end")
        try:
            cond = next(c for c in self.cond_list if c.name == self.var_cond.get())
            amb = float(self.var_amb.get())
            mot = float(self.var_mot.get())

            self.rating.select_conductor_solve(self.var_season.get(), cond, amb, mot)

            self.output.insert("end", f"Conductor: {cond.name}\n")
            self.output.insert("end", f"Season: {self.var_season.get()}\n")
            self.output.insert("end", f"Ambient: {amb:.2f} C\n")
            self.output.insert("end", f"MOT: {mot:.2f} C\n\n")
            self.output.insert("end", f"Rating A: {self.rating.rate_a:.1f} A\n")
            self.output.insert("end", f"Rating B: {self.rating.rate_b:.1f} A\n")
            self.output.insert("end", f"Rating C: {self.rating.rate_c:.1f} A\n\n")
            self.output.insert(
                "end",
                "Max MOTs: ACSR and CU=125C, CU-Hytherm=150C, ACCC=200C, ACCR=240C, ACSS=250C\n",
            )
        except Exception as ex:
            self.output.insert("end", f"Calculation error: {ex}")
