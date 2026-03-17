"""Compatibility module mapped from AboutBox.cs."""

# Legacy compatibility shim for old CamelCase module references.
from .about_box import AboutDialog

__all__ = ["AboutDialog"]
