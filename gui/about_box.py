"""About dialog for the Python TLIC port."""

from __future__ import annotations

import tkinter as tk
from tkinter import ttk


class AboutDialog(tk.Toplevel):
    def __init__(self, parent: tk.Misc) -> None:
        super().__init__(parent)
        self.title("About TLIC")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        body = ttk.Frame(self, padding=14)
        body.grid(sticky="nsew")

        ttk.Label(body, text="TLIC (Python Port)", font=("Segoe UI", 11, "bold")).grid(row=0, column=0, sticky="w")
        ttk.Label(body, text="Phase 2 migration build").grid(row=1, column=0, sticky="w", pady=(4, 0))
        ttk.Label(
            body,
            text="Includes line sections, ratings, impedance output, project XML, script export, structure editor, and sweeper.",
            wraplength=420,
            justify="left",
        ).grid(row=2, column=0, sticky="w", pady=(2, 10))
        ttk.Button(body, text="Close", command=self.destroy).grid(row=3, column=0, sticky="e")
