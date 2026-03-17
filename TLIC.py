"""TLIC Python entry point.

PHASE 1 GOAL:
- Open a GUI shell that is ready for feature migration from C#.
- No domain calculations are implemented yet.
"""

# Keep this file tiny on purpose:
# - packaging/launch tools expect a top-level executable module
# - all real startup work lives in gui.program
from gui.program import main


if __name__ == "__main__":
    # Use SystemExit(main()) so callers get an exit code and this module still
    # behaves like a regular command-line program.
    raise SystemExit(main())
