"""Application bootstrap for the Python TLIC port."""

import tkinter as tk

from .main_form import MainForm


def main() -> int:
    root = tk.Tk()
    root.title("TLIC (Python Port) - Phase 2")
    root.geometry("1440x900")
    root.minsize(1180, 720)

    app = MainForm(root)
    app.pack(fill="both", expand=True)

    root.mainloop()
    return 0
