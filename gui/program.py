"""Application bootstrap for the Python TLIC port."""

import tkinter as tk

from .main_form import MainForm


def main() -> int:
    # Root window setup mirrors the C# app’s "big desktop utility" footprint.
    root = tk.Tk()
    root.title("TLIC (Python Port) - Phase 2")
    # Start large enough to show all panes without manual resize.
    root.geometry("1440x900")
    # Guardrail so controls/plot/output stay usable.
    root.minsize(1180, 720)

    # MainForm owns all tab-level application behavior.
    app = MainForm(root)
    app.pack(fill="both", expand=True)

    root.mainloop()
    return 0
