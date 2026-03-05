"""Compatibility module mapped from MainForm.cs."""

# This shim keeps legacy import paths working after the snake_case migration.
# External scripts can still import gui.MainForm without knowing internal file
# naming changed to main_form.py.
from .main_form import MainForm

__all__ = ["MainForm"]
