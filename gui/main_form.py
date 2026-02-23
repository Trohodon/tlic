"""Main application window for the Python TLIC port (Phase 2)."""

from __future__ import annotations

import os
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from .about_box import AboutDialog
from .line_rate_main_form import LineRateMainForm
from .structure_builder import StructureBuilder, StructureEditorDialog
from .tline_variable_sweeper import TLineVariableSweeper
from core.branch_engine import BranchEngine
from core.exporters import build_aux_script, build_python_script
from core.line_rating_engine import LineRatingCalc
from core.project_io import load_project_xml, save_project_xml
from core.tlic_data import by_name, load_conductors, load_structures, sample_conductors, sample_statics, sample_structures
from core.tlic_models import BranchOptions, Conductor, LineSection, ProjectData, Structure


class MainForm(ttk.Frame):
    def __init__(self, parent: tk.Misc) -> None:
        super().__init__(parent)
        self.parent = parent

        self.kvs = [0.48, 0.6, 2.4, 4.0, 8.0, 12.0, 13.0, 13.8, 23.0, 33.0, 46.0, 69.0, 115.0, 230.0, 500.0]
        self.temps = [26.7, 40.0]

        self.project = ProjectData(options=BranchOptions())
        self.phase_conds: list[Conductor] = sample_conductors()
        self.static_conds: list[Conductor] = sample_statics()
        self.structures: list[Structure] = sample_structures()

        self.external_cond_path = ""
        self.external_struct_path = ""

        self.rating_calc = LineRatingCalc()
        self.branch_engine = BranchEngine(self.rating_calc)
        self.last_result = None

        self.status_var = tk.StringVar(value="Ready")

        self._build_ui()
        self._load_default_data()
        self._refresh_selectors()
        self.recalculate()

    def _build_ui(self) -> None:
        self.columnconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)

        self._build_menu()

        top = ttk.Frame(self, padding=(10, 8))
        top.grid(row=0, column=0, sticky="ew")
        top.columnconfigure(1, weight=1)
        ttk.Label(top, text="TLIC Python Port", font=("Segoe UI", 13, "bold")).grid(row=0, column=0, sticky="w")
        ttk.Label(top, text="Phase 2", foreground="#0d47a1", font=("Segoe UI", 11, "bold")).grid(row=0, column=2, sticky="e")

        tabs = ttk.Notebook(self)
        tabs.grid(row=1, column=0, sticky="nsew", padx=10, pady=(0, 8))

        self.tab_main = ttk.Frame(tabs, padding=10)
        tabs.add(self.tab_main, text="Main")
        self._build_main_tab()

        self.line_rate_tab = LineRateMainForm(tabs)
        tabs.add(self.line_rate_tab, text="Line Rating")

        self.structure_tab = StructureBuilder(tabs)
        tabs.add(self.structure_tab, text="Structure Builder")

        self.sweeper_tab = TLineVariableSweeper(tabs)
        tabs.add(self.sweeper_tab, text="Variable Sweeper")

        status = ttk.Label(self, textvariable=self.status_var, anchor="w")
        status.grid(row=2, column=0, sticky="ew", padx=10, pady=(0, 8))

    def _build_menu(self) -> None:
        menu = tk.Menu(self.parent)

        file_menu = tk.Menu(menu, tearoff=0)
        file_menu.add_command(label="Open Project...", command=self.on_open)
        file_menu.add_command(label="Save Project...", command=self.on_save)
        file_menu.add_separator()
        file_menu.add_command(label="Close Project", command=self.on_close_project)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self.parent.destroy)
        menu.add_cascade(label="File", menu=file_menu)

        export_menu = tk.Menu(menu, tearoff=0)
        export_menu.add_command(label="Build Python Script...", command=self.on_export_python)
        export_menu.add_command(label="Build AUX Script...", command=self.on_export_aux)
        menu.add_cascade(label="Export", menu=export_menu)

        help_menu = tk.Menu(menu, tearoff=0)
        help_menu.add_command(label="About", command=self._open_about)
        menu.add_cascade(label="Help", menu=help_menu)

        self.parent.configure(menu=menu)

    def _build_main_tab(self) -> None:
        self.tab_main.columnconfigure(1, weight=1)
        self.tab_main.rowconfigure(0, weight=1)

        left = ttk.Frame(self.tab_main)
        left.grid(row=0, column=0, sticky="nsw")

        right = ttk.Frame(self.tab_main)
        right.grid(row=0, column=1, sticky="nsew", padx=(10, 0))
        right.columnconfigure(0, weight=1)
        right.rowconfigure(1, weight=1)
        right.rowconfigure(3, weight=1)

        self._build_left_controls(left)
        self._build_right_views(right)

    def _build_left_controls(self, parent: ttk.Frame) -> None:
        row = 0

        files = ttk.LabelFrame(parent, text="Data Files", padding=8)
        files.grid(row=row, column=0, sticky="ew")
        files.columnconfigure(1, weight=1)

        self.cond_file_var = tk.StringVar(value="")
        self.struct_file_var = tk.StringVar(value="")

        ttk.Label(files, text="Conductor").grid(row=0, column=0, sticky="w")
        ttk.Entry(files, textvariable=self.cond_file_var, width=36).grid(row=0, column=1, sticky="ew", padx=4)
        ttk.Button(files, text="Browse", command=self.on_browse_cond).grid(row=0, column=2)

        ttk.Label(files, text="Structure").grid(row=1, column=0, sticky="w")
        ttk.Entry(files, textvariable=self.struct_file_var, width=36).grid(row=1, column=1, sticky="ew", padx=4)
        ttk.Button(files, text="Browse", command=self.on_browse_struct).grid(row=1, column=2)

        row += 1
        selection = ttk.LabelFrame(parent, text="Selection", padding=8)
        selection.grid(row=row, column=0, sticky="ew", pady=(8, 0))
        selection.columnconfigure(1, weight=1)

        self.cond_var = tk.StringVar()
        self.static_var = tk.StringVar()
        self.struct_var = tk.StringVar()
        self.mot_var = tk.StringVar(value="125")
        self.temp_var = tk.StringVar(value="40.0")
        self.mileage_var = tk.StringVar(value="1.0")
        self.feet_var = tk.StringVar(value=str(5280.0))

        ttk.Label(selection, text="Conductor").grid(row=0, column=0, sticky="w")
        self.cmb_cond = ttk.Combobox(selection, textvariable=self.cond_var, state="readonly", width=34)
        self.cmb_cond.grid(row=0, column=1, sticky="ew", padx=4, pady=2)
        self.cmb_cond.bind("<<ComboboxSelected>>", lambda _e: self.on_cond_or_season_change())

        ttk.Label(selection, text="Static").grid(row=1, column=0, sticky="w")
        self.cmb_static = ttk.Combobox(selection, textvariable=self.static_var, state="readonly", width=34)
        self.cmb_static.grid(row=1, column=1, sticky="ew", padx=4, pady=2)
        self.cmb_static.bind("<<ComboboxSelected>>", lambda _e: self.on_static_change())

        ttk.Label(selection, text="Structure").grid(row=2, column=0, sticky="w")
        self.cmb_struct = ttk.Combobox(selection, textvariable=self.struct_var, state="readonly", width=34)
        self.cmb_struct.grid(row=2, column=1, sticky="ew", padx=4, pady=2)
        self.cmb_struct.bind("<<ComboboxSelected>>", lambda _e: self.on_structure_change())

        ttk.Label(selection, text="MOT (C)").grid(row=3, column=0, sticky="w")
        mot = ttk.Entry(selection, textvariable=self.mot_var, width=10)
        mot.grid(row=3, column=1, sticky="w", padx=4, pady=2)
        mot.bind("<Return>", lambda _e: self.on_cond_or_season_change())

        ttk.Label(selection, text="Ambient (C)").grid(row=4, column=0, sticky="w")
        cmb_temp = ttk.Combobox(selection, textvariable=self.temp_var, values=[str(t) for t in self.temps], width=10)
        cmb_temp.grid(row=4, column=1, sticky="w", padx=4, pady=2)
        cmb_temp.bind("<<ComboboxSelected>>", lambda _e: self.recalculate())

        ttk.Label(selection, text="Mileage").grid(row=5, column=0, sticky="w")
        mil = ttk.Entry(selection, textvariable=self.mileage_var, width=12)
        mil.grid(row=5, column=1, sticky="w", padx=4, pady=2)
        mil.bind("<FocusOut>", lambda _e: self._sync_feet_from_miles())
        mil.bind("<Return>", lambda _e: self._sync_feet_from_miles())

        ttk.Label(selection, text="Feet").grid(row=6, column=0, sticky="w")
        feet = ttk.Entry(selection, textvariable=self.feet_var, width=12)
        feet.grid(row=6, column=1, sticky="w", padx=4, pady=2)
        feet.bind("<FocusOut>", lambda _e: self._sync_miles_from_feet())
        feet.bind("<Return>", lambda _e: self._sync_miles_from_feet())

        self.season_var = tk.StringVar(value="Summer")
        ttk.Radiobutton(selection, text="Summer", value="Summer", variable=self.season_var, command=self.on_cond_or_season_change).grid(
            row=7, column=0, sticky="w", pady=(4, 0)
        )
        ttk.Radiobutton(selection, text="Winter", value="Winter", variable=self.season_var, command=self.on_cond_or_season_change).grid(
            row=7, column=1, sticky="w", pady=(4, 0)
        )

        row += 1
        branch = ttk.LabelFrame(parent, text="Branch Options", padding=8)
        branch.grid(row=row, column=0, sticky="ew", pady=(8, 0))
        branch.columnconfigure(1, weight=1)

        self.bus1_var = tk.StringVar(value="1")
        self.bus2_var = tk.StringVar(value="2")
        self.ckt_var = tk.StringVar(value="1")
        self.status_open_var = tk.BooleanVar(value=True)
        self.kv_var = tk.StringVar(value="230")
        self.mva_base_var = tk.StringVar(value="100")
        self.rho_var = tk.StringVar(value="100")
        self.line_name_var = tk.StringVar(value="")
        self.bus1_name_var = tk.StringVar(value="")
        self.bus2_name_var = tk.StringVar(value="")
        self.include_seq_var = tk.BooleanVar(value=True)

        labels = [
            ("Line Name", self.line_name_var),
            ("Bus1", self.bus1_var),
            ("Bus1 Name", self.bus1_name_var),
            ("Bus2", self.bus2_var),
            ("Bus2 Name", self.bus2_name_var),
            ("Circuit", self.ckt_var),
            ("KV", self.kv_var),
            ("MVA Base", self.mva_base_var),
            ("Rho", self.rho_var),
        ]

        for i, (lab, var) in enumerate(labels):
            ttk.Label(branch, text=lab).grid(row=i, column=0, sticky="w")
            ent = ttk.Entry(branch, textvariable=var, width=16)
            ent.grid(row=i, column=1, sticky="ew", padx=4, pady=1)
            ent.bind("<FocusOut>", lambda _e: self.recalculate())

        ttk.Checkbutton(branch, text="In Service", variable=self.status_open_var, command=self.recalculate).grid(
            row=9, column=0, sticky="w", pady=(4, 0)
        )
        ttk.Checkbutton(branch, text="Include Seq in Python Export", variable=self.include_seq_var).grid(
            row=9, column=1, sticky="w", pady=(4, 0)
        )

        row += 1
        actions = ttk.Frame(parent)
        actions.grid(row=row, column=0, sticky="ew", pady=(8, 0))

        ttk.Button(actions, text="Add Section", command=self.on_add_section).pack(side="left")
        ttk.Button(actions, text="Delete Selected", command=self.on_delete_selected).pack(side="left", padx=4)
        ttk.Button(actions, text="Clear Sections", command=self.on_clear_sections).pack(side="left", padx=4)
        ttk.Button(actions, text="Edit Structure", command=self.on_structure_edit).pack(side="left", padx=4)
        ttk.Button(actions, text="Recalculate", command=self.recalculate).pack(side="left", padx=4)

    def _build_right_views(self, parent: ttk.Frame) -> None:
        struct_box = ttk.LabelFrame(parent, text="Structure Plot", padding=6)
        struct_box.grid(row=0, column=0, sticky="nsew")
        struct_box.columnconfigure(0, weight=1)
        struct_box.rowconfigure(0, weight=1)

        self.struct_canvas = tk.Canvas(struct_box, background="#fefefe", height=230, highlightthickness=1, highlightbackground="#ddd")
        self.struct_canvas.grid(row=0, column=0, sticky="nsew")

        sections_box = ttk.LabelFrame(parent, text="Line Sections", padding=6)
        sections_box.grid(row=1, column=0, sticky="nsew", pady=(8, 0))
        sections_box.columnconfigure(0, weight=1)
        sections_box.rowconfigure(0, weight=1)

        cols = ("Struct", "Conductor", "Static", "MOT", "Mileage", "Custom")
        self.tree_sections = ttk.Treeview(sections_box, columns=cols, show="headings", selectmode="extended")
        for c in cols:
            self.tree_sections.heading(c, text=c)
        self.tree_sections.column("Struct", width=100, anchor="w")
        self.tree_sections.column("Conductor", width=230, anchor="w")
        self.tree_sections.column("Static", width=180, anchor="w")
        self.tree_sections.column("MOT", width=70, anchor="center")
        self.tree_sections.column("Mileage", width=80, anchor="e")
        self.tree_sections.column("Custom", width=70, anchor="center")
        self.tree_sections.grid(row=0, column=0, sticky="nsew")

        ysb = ttk.Scrollbar(sections_box, orient="vertical", command=self.tree_sections.yview)
        ysb.grid(row=0, column=1, sticky="ns")
        self.tree_sections.configure(yscrollcommand=ysb.set)

        desc_box = ttk.LabelFrame(parent, text="Conductor/Static Details", padding=6)
        desc_box.grid(row=2, column=0, sticky="nsew", pady=(8, 0))
        desc_box.columnconfigure(0, weight=1)
        desc_box.columnconfigure(1, weight=1)
        desc_box.rowconfigure(0, weight=1)

        self.cond_desc = tk.Text(desc_box, height=8, wrap="word")
        self.cond_desc.grid(row=0, column=0, sticky="nsew", padx=(0, 4))
        self.static_desc = tk.Text(desc_box, height=8, wrap="word")
        self.static_desc.grid(row=0, column=1, sticky="nsew", padx=(4, 0))

        out_box = ttk.LabelFrame(parent, text="Impedance Calculation Output", padding=6)
        out_box.grid(row=3, column=0, sticky="nsew", pady=(8, 0))
        out_box.columnconfigure(0, weight=1)
        out_box.rowconfigure(0, weight=1)

        self.output = tk.Text(out_box, wrap="none")
        self.output.grid(row=0, column=0, sticky="nsew")
        y2 = ttk.Scrollbar(out_box, orient="vertical", command=self.output.yview)
        y2.grid(row=0, column=1, sticky="ns")
        x2 = ttk.Scrollbar(out_box, orient="horizontal", command=self.output.xview)
        x2.grid(row=1, column=0, sticky="ew")
        self.output.configure(yscrollcommand=y2.set, xscrollcommand=x2.set)

        ctx = tk.Menu(self.output, tearoff=0)
        ctx.add_command(label="Copy Selected", command=self._copy_output_selection)
        ctx.add_command(label="Select All", command=lambda: self.output.tag_add("sel", "1.0", "end"))
        self.output.bind("<Button-3>", lambda e: ctx.tk_popup(e.x_root, e.y_root))

    def _resource_path(self, filename: str) -> str:
        return os.path.join(os.getcwd(), "Resources", filename)

    def _load_default_data(self) -> None:
        # Primary shared-source defaults requested by user.
        network_root = (
            r"\\mbu.ad.dominionnet.com\data\TRANSMISSION OPERATIONS CENTER\7T\DATA2"
            r"\DESC_Trans_Planning\LTR_General\SOFTWARE\_IN HOUSE\TLICs\Source\TLICs\Resources"
        )

        cond_default = os.path.join(network_root, "conddata.csv")
        struct_default = os.path.join(network_root, "structdata.txt")

        # Fallback to local repo Resources if UNC path is not available.
        if not os.path.exists(cond_default):
            cond_default = self._resource_path("conddata.csv")
        if not os.path.exists(struct_default):
            struct_default = self._resource_path("structdata.txt")

        self.cond_file_var.set(cond_default if os.path.exists(cond_default) else "(sample data)")
        self.struct_file_var.set(struct_default if os.path.exists(struct_default) else "(sample data)")

        self.phase_conds, self.static_conds = load_conductors(cond_default)
        self.structures = load_structures(struct_default)

    def _refresh_selectors(self) -> None:
        cond_names = [c.name for c in self.phase_conds]
        static_names = [s.name for s in self.static_conds]

        # Preserve legacy behavior: include both types after separator.
        cond_combo = cond_names + ["----------"] + static_names
        static_combo = static_names + ["----------"] + cond_names

        self.cmb_cond.configure(values=cond_combo)
        self.cmb_static.configure(values=static_combo)

        struct_display = []
        self._struct_display_map: dict[str, str] = {}
        for s in self.structures + list(self.project.custom_structures.values()):
            label = f"{s.name:<11} ({s.es:.2f})"
            struct_display.append(label)
            self._struct_display_map[label] = s.name

        self.cmb_struct.configure(values=struct_display)

        if not self.cond_var.get() and cond_combo:
            self.cond_var.set(cond_combo[0])
        if not self.static_var.get() and static_combo:
            self.static_var.set(static_combo[0])
        if not self.struct_var.get() and struct_display:
            self.struct_var.set(struct_display[0])

        self.on_structure_change()
        self.on_cond_or_season_change()

    def on_browse_cond(self) -> None:
        path = filedialog.askopenfilename(
            title="Select conductor data file",
            filetypes=[("CSV/TXT", "*.csv *.txt"), ("All files", "*.*")],
        )
        if not path:
            return
        self.external_cond_path = path
        self.cond_file_var.set(path)
        self.phase_conds, self.static_conds = load_conductors(path)
        self._refresh_selectors()
        self.recalculate()
        self.status_var.set(f"Loaded conductor data: {os.path.basename(path)}")

    def on_browse_struct(self) -> None:
        path = filedialog.askopenfilename(
            title="Select structure data file",
            filetypes=[("Text", "*.txt *.csv"), ("All files", "*.*")],
        )
        if not path:
            return
        self.external_struct_path = path
        self.struct_file_var.set(path)
        self.structures = load_structures(path)
        self._refresh_selectors()
        self.recalculate()
        self.status_var.set(f"Loaded structure data: {os.path.basename(path)}")

    def _selected_structure_name(self) -> str:
        val = self.struct_var.get().strip()
        if val in self._struct_display_map:
            return self._struct_display_map[val]
        return val.split()[0] if val else ""

    def _selected_structure(self) -> Structure | None:
        name = self._selected_structure_name()
        if not name:
            return None
        if name in self.project.custom_structures:
            return self.project.custom_structures[name]
        return by_name(self.structures, name)

    def on_structure_change(self) -> None:
        self._draw_structure(self._selected_structure())

    def on_cond_or_season_change(self) -> None:
        cond = by_name(self.phase_conds + self.static_conds, self.cond_var.get())
        if cond is None:
            return

        self.cond_desc.delete("1.0", "end")
        self.cond_desc.insert("end", f"gmr:\t{cond.gmr_ft:.4f} ft\n")
        self.cond_desc.insert("end", f"rad:\t{cond.radius_ft:.4f} ft\n")
        self.cond_desc.insert("end", f"R:\t{cond.r_ohm_per_mi:.4f} ohm/mi\n")
        self.cond_desc.insert("end", f"XL:\t{cond.xl_ohm_per_mi:.4f} ohm/mi\n")
        self.cond_desc.insert("end", f"XC:\t{cond.xc_mohm_mi:.4f} Mohm-mi\n")

        is_summer = self.season_var.get() == "Summer"
        amb = float(self.temp_var.get() or 40)
        mot = float(self.mot_var.get() or 125)

        self.rating_calc.select_conductor_solve(is_summer, cond, amb, mot)
        self.cond_desc.insert("end", f"{self.season_var.get()[:3]} Rating A:\t{int(self.rating_calc.rate_a)} A\n")
        self.cond_desc.insert("end", f"{self.season_var.get()[:3]} Rating B:\t{int(self.rating_calc.rate_b)} A\n")
        self.cond_desc.insert("end", f"{self.season_var.get()[:3]} Rating C:\t{int(self.rating_calc.rate_c)} A\n")

        other = not is_summer
        self.rating_calc.select_conductor_solve(other, cond, amb, mot)
        season = "Win" if is_summer else "Sum"
        self.cond_desc.insert("end", f"{season} Rating A:\t{int(self.rating_calc.rate_a)} A\n")
        self.cond_desc.insert("end", f"{season} Rating B:\t{int(self.rating_calc.rate_b)} A\n")
        self.cond_desc.insert("end", f"{season} Rating C:\t{int(self.rating_calc.rate_c)} A\n")
        self.cond_desc.insert(
            "end",
            "\nMax MOTs: ACSR and CU=125C, CU-Hytherm=150C, ACCC=200C, ACCR=240C, ACSS=250C\n",
        )

        self.on_static_change()

    def on_static_change(self) -> None:
        st = by_name(self.phase_conds + self.static_conds, self.static_var.get())
        if st is None:
            return
        self.static_desc.delete("1.0", "end")
        self.static_desc.insert("end", f"gmr:\t{st.gmr_ft:.4f} ft\n")
        self.static_desc.insert("end", f"rad:\t{st.radius_ft:.4f} ft\n")
        self.static_desc.insert("end", f"R:\t{st.r_ohm_per_mi:.4f} ohm/mi\n")
        self.static_desc.insert("end", f"XL:\t{st.xl_ohm_per_mi:.4f} ohm/mi\n")
        self.static_desc.insert("end", f"XC:\t{st.xc_mohm_mi:.4f} Mohm-mi\n")
        self.static_desc.insert("end", f"Amp A:\t{st.rate_a:.0f} A\n")
        self.static_desc.insert("end", f"Amp B:\t{st.rate_b:.0f} A\n")
        self.static_desc.insert("end", f"Amp C:\t{st.rate_c:.0f} A\n")

    def _sync_feet_from_miles(self) -> None:
        try:
            self.feet_var.set(f"{float(self.mileage_var.get()) * 5280:.4f}")
        except Exception:
            pass

    def _sync_miles_from_feet(self) -> None:
        try:
            self.mileage_var.set(f"{float(self.feet_var.get()) / 5280:.6f}")
        except Exception:
            pass

    def _collect_options(self) -> BranchOptions:
        return BranchOptions(
            bus1=int(float(self.bus1_var.get() or 1)),
            bus2=int(float(self.bus2_var.get() or 2)),
            ckt=(self.ckt_var.get() or "1"),
            in_service=self.status_open_var.get(),
            kv=float(self.kv_var.get() or 230),
            mva_base=float(self.mva_base_var.get() or 100),
            temp_c=float(self.temp_var.get() or 40),
            rho=float(self.rho_var.get() or 100),
            line_name=self.line_name_var.get(),
            bus1_name=self.bus1_name_var.get(),
            bus2_name=self.bus2_name_var.get(),
        )

    def on_add_section(self) -> None:
        try:
            sec = LineSection(
                cond_name=self.cond_var.get().strip(),
                static_name=self.static_var.get().strip(),
                struct_name=self._selected_structure_name(),
                mileage=float(self.mileage_var.get() or 1.0),
                is_custom_structure=self._selected_structure_name() in self.project.custom_structures,
                mot=float(self.mot_var.get() or 125),
            )
            self.project.sections.append(sec)
            self._refresh_sections_grid()
            self.recalculate()
        except Exception as ex:
            messagebox.showerror("Add Section", f"Could not add section: {ex}")

    def on_delete_selected(self) -> None:
        selected = list(self.tree_sections.selection())
        if not selected:
            messagebox.showwarning("Delete Selected", "No selected sections to delete.")
            return
        if not messagebox.askokcancel("Delete Selected", "Delete selected line section(s)?"):
            return

        idxs = sorted((self.tree_sections.index(i) for i in selected), reverse=True)
        for idx in idxs:
            if 0 <= idx < len(self.project.sections):
                self.project.sections.pop(idx)

        self._refresh_sections_grid()
        self.recalculate()

    def on_clear_sections(self) -> None:
        if not messagebox.askyesno("Confirm", "Clear all recorded line sections?"):
            return
        self.project.sections.clear()
        self._refresh_sections_grid()
        self.recalculate()

    def on_structure_edit(self) -> None:
        current = self._selected_structure()
        if current is None:
            messagebox.showwarning("Structure Editor", "Select a structure first.")
            return
        dlg = StructureEditorDialog(self.parent, current)
        self.wait_window(dlg)
        if dlg.result is None:
            return

        self.project.custom_structures[dlg.result.name] = dlg.result
        self.structures = [s for s in self.structures if s.name != dlg.result.name]
        self._refresh_selectors()
        # Select edited structure label.
        for label, name in self._struct_display_map.items():
            if name == dlg.result.name:
                self.struct_var.set(label)
                break
        self.on_structure_change()
        self.status_var.set(f"Custom structure saved: {dlg.result.name}")

    def _refresh_sections_grid(self) -> None:
        for iid in self.tree_sections.get_children():
            self.tree_sections.delete(iid)

        for sec in self.project.sections:
            self.tree_sections.insert(
                "",
                "end",
                values=(
                    sec.struct_name,
                    sec.cond_name,
                    sec.static_name,
                    f"{sec.mot:.1f}",
                    f"{sec.mileage:.3f}",
                    "True" if sec.is_custom_structure else "False",
                ),
            )

    def recalculate(self) -> None:
        try:
            self.project.options = self._collect_options()
            structs = self.structures + list(self.project.custom_structures.values())
            self.last_result = self.branch_engine.calculate(
                self.project.options,
                self.project.sections,
                self.phase_conds,
                self.static_conds,
                structs,
                is_summer=self.season_var.get() == "Summer",
            )
            self._render_output()
            self.status_var.set("Calculation complete")
        except Exception as ex:
            self.status_var.set(f"Calculation failed: {ex}")

    def _render_output(self) -> None:
        out = self.output
        out.delete("1.0", "end")

        if not self.project.sections:
            out.insert("end", "No Data\n")
            return

        res = self.last_result
        if res is None:
            out.insert("end", "No result\n")
            return

        lines = []
        lines.append("Impedance Calculation Output:\n")
        lines.append("Struct    Conductor                   Static                  MOT    Mileage")
        lines.append("--------------------------------------------------------------------------")
        for sec in self.project.sections:
            lines.append(
                f"{sec.struct_name:<9}{sec.cond_name:<28}{sec.static_name:<24}{sec.mot:<7.1f}{sec.mileage:.2f}"
            )

        lines.extend(
            [
                "",
                f"Total Length:\t\t{res.length_mi:.2f} mi",
                f"Rating A:\t\t{res.mva_rating_a(self.project.options.kv):.2f} MVA",
                f"Rating B:\t\t{res.mva_rating_b(self.project.options.kv):.2f} MVA",
                f"Rating C:\t\t{res.mva_rating_c(self.project.options.kv):.2f} MVA",
                f"Nominal Voltage:\t{self.project.options.kv:.2f} kV",
                f"MVA Base:\t\t{self.project.options.mva_base:.1f} MVA",
                "",
                "Per Unit Positive Sequence Impedances:",
                "----------------------------------------",
                f"R: {res.r1_pu:.6f} p.u.",
                f"X: {res.x1_pu:.6f} p.u.",
                f"B: {res.b1_pu:.6f} p.u.",
                "",
                "Per Unit Zero Sequence Impedances:",
                "-----------------------------------",
                f"R0: {res.r0_pu:.6f} p.u.",
                f"X0: {res.x0_pu:.6f} p.u.",
                f"B0: {res.b0_pu:.6f} p.u.",
                "",
                "Per Mile Impedances:",
                "-----------------------------------",
            ]
        )

        if len(self.project.sections) == 1:
            lines.extend(
                [
                    f"Z1: {res.z1_per_mile_r:.6f} + j{res.z1_per_mile_x:.6f} ohm/mi",
                    f"Y1: 0.000000 + j{res.y1_per_mile_b:.6f} us/mi",
                    "",
                    f"Z0: {res.z0_per_mile_r:.6f} + j{res.z0_per_mile_x:.6f} ohm/mi",
                    f"Y0: 0.000000 + j{res.y0_per_mile_b:.6f} us/mi",
                    "",
                ]
            )
        else:
            lines.append("(per mile impedances are not applicable to multi-section branches)")
            lines.append("")

        lines.extend(
            [
                "PSS/E Format:",
                "------------------------------",
                f"raw string:\n{res.raw_string} / {self.project.options.bus1_name} {self.project.options.bus2_name}",
                "",
                f"seq string:\n{res.seq_string}",
                "",
                "NOTE: impedance/rating results are computed using a Python-engine approximation,",
                "because TLILib/TLineCalc binaries used by the original C# project are not present.",
            ]
        )

        out.insert("end", "\n".join(lines))

    def _draw_structure(self, structure: Structure | None) -> None:
        c = self.struct_canvas
        c.delete("all")
        if structure is None:
            return

        pts = structure.a[:] + [p for p in structure.g if p.y != 0.0]
        w = max(c.winfo_width(), 350)
        h = max(c.winfo_height(), 220)
        pad = 36

        xs = [p.x for p in pts] + [0.0]
        ys = [p.y for p in pts] + [0.0]
        raw_x_min, raw_x_max = min(xs), max(xs)
        raw_y_min, raw_y_max = min(ys), max(ys)

        x_span = max(raw_x_max - raw_x_min, 1.0)
        y_span = max(raw_y_max - raw_y_min, 1.0)
        x_pad = max(x_span * 0.15, 2.0)
        y_pad = max(y_span * 0.15, 2.0)

        x_min = raw_x_min - x_pad
        x_max = raw_x_max + x_pad
        y_min = min(0.0, raw_y_min - y_pad)
        y_max = raw_y_max + y_pad

        def tx(x: float) -> float:
            return pad + (x - x_min) * (w - 2 * pad) / (x_max - x_min)

        def ty(y: float) -> float:
            return h - pad - (y - y_min) * (h - 2 * pad) / (y_max - y_min)

        # Draw axes.
        c.create_line(tx(0), ty(y_min), tx(0), ty(y_max), fill="#ddd", width=2)
        c.create_line(tx(x_min), ty(0), tx(x_max), ty(0), fill="#ddd", width=1)

        # Draw dynamic ticks/labels based on structure bounds.
        tick_count = 6
        for i in range(tick_count + 1):
            xv = x_min + (x_max - x_min) * i / tick_count
            xpix = tx(xv)
            c.create_line(xpix, ty(0) - 4, xpix, ty(0) + 4, fill="#999")
            c.create_text(xpix, ty(0) + 14, text=f"{xv:.1f}", fill="#666", anchor="n", font=("Segoe UI", 8))

            yv = y_min + (y_max - y_min) * i / tick_count
            ypix = ty(yv)
            c.create_line(tx(0) - 4, ypix, tx(0) + 4, ypix, fill="#999")
            c.create_text(tx(0) - 8, ypix, text=f"{yv:.1f}", fill="#666", anchor="e", font=("Segoe UI", 8))

        for lbl, p in zip(["A", "B", "C"], structure.a):
            x, y = tx(p.x), ty(p.y)
            c.create_oval(x - 5, y - 5, x + 5, y + 5, fill="#d32f2f", outline="")
            c.create_text(x + 8, y - 8, text=lbl, anchor="w", fill="#333")

        for i, p in enumerate(structure.g):
            if p.y == 0.0:
                continue
            x, y = tx(p.x), ty(p.y)
            c.create_oval(x - 4, y - 4, x + 4, y + 4, fill="#1565c0", outline="")
            c.create_text(x + 8, y - 8, text=f"G{i + 1}", anchor="w", fill="#333")
        c.create_text(
            tx(x_min) + 2,
            ty(y_min) + 2,
            text=f"X: {x_min:.1f}..{x_max:.1f}  Y: {y_min:.1f}..{y_max:.1f}",
            anchor="nw",
            fill="#666",
            font=("Segoe UI", 8),
        )

    def _copy_output_selection(self) -> None:
        try:
            txt = self.output.get("sel.first", "sel.last")
            self.clipboard_clear()
            self.clipboard_append(txt)
        except tk.TclError:
            return

    def on_save(self) -> None:
        path = filedialog.asksaveasfilename(
            title="Save Project",
            defaultextension=".xml",
            filetypes=[("XML files", "*.xml"), ("All files", "*.*")],
        )
        if not path:
            return
        try:
            self.project.options = self._collect_options()
            save_project_xml(path, self.project)
            self.status_var.set(f"Saved project: {os.path.basename(path)}")
        except Exception as ex:
            messagebox.showerror("Save", f"Could not save project: {ex}")

    def on_open(self) -> None:
        path = filedialog.askopenfilename(
            title="Open Project",
            filetypes=[("XML files", "*.xml"), ("All files", "*.*")],
        )
        if not path:
            return
        try:
            self.project = load_project_xml(path)
            self._apply_options_to_ui(self.project.options)
            self._refresh_selectors()
            self._refresh_sections_grid()
            self.recalculate()
            self.status_var.set(f"Opened project: {os.path.basename(path)}")
        except Exception as ex:
            messagebox.showerror("Open", f"Could not open project: {ex}")

    def _apply_options_to_ui(self, opts: BranchOptions) -> None:
        self.bus1_var.set(str(opts.bus1))
        self.bus2_var.set(str(opts.bus2))
        self.ckt_var.set(opts.ckt)
        self.status_open_var.set(opts.in_service)
        self.kv_var.set(str(opts.kv))
        self.mva_base_var.set(str(opts.mva_base))
        self.temp_var.set(str(opts.temp_c))
        self.rho_var.set(str(opts.rho))
        self.line_name_var.set(opts.line_name)
        self.bus1_name_var.set(opts.bus1_name)
        self.bus2_name_var.set(opts.bus2_name)

    def on_close_project(self) -> None:
        if not messagebox.askyesno("Close Project", "Reset all values to defaults and clear sections?"):
            return
        self.project = ProjectData(options=BranchOptions())
        self._apply_options_to_ui(self.project.options)
        self.mileage_var.set("1.0")
        self.mot_var.set("125")
        self._sync_feet_from_miles()
        self._refresh_sections_grid()
        self.recalculate()

    def on_export_python(self) -> None:
        if not self.project.sections or self.last_result is None:
            messagebox.showinfo("Export", "Cannot create output file. No line sections in list.")
            return

        path = filedialog.asksaveasfilename(
            title="Build Python File",
            defaultextension=".py",
            filetypes=[("Python Script", "*.py"), ("All files", "*.*")],
        )
        if not path:
            return

        try:
            content = build_python_script(self._collect_options(), self.last_result, self.include_seq_var.get())
            self._write_with_overwrite_or_append(path, content)
            self.status_var.set(f"Python script written: {os.path.basename(path)}")
        except Exception as ex:
            messagebox.showerror("Export", f"Could not write python script: {ex}")

    def on_export_aux(self) -> None:
        if not self.project.sections or self.last_result is None:
            messagebox.showinfo("Export", "Cannot create output file. No line sections in list.")
            return

        path = filedialog.asksaveasfilename(
            title="Build Aux File",
            defaultextension=".aux",
            filetypes=[("PowerWorld Auxiliary", "*.aux"), ("All files", "*.*")],
        )
        if not path:
            return

        try:
            content = build_aux_script(self._collect_options(), self.last_result)
            self._write_with_overwrite_or_append(path, content)
            self.status_var.set(f"AUX script written: {os.path.basename(path)}")
        except Exception as ex:
            messagebox.showerror("Export", f"Could not write aux script: {ex}")

    def _write_with_overwrite_or_append(self, path: str, content: str) -> None:
        mode = "w"
        if os.path.exists(path):
            choice = messagebox.askyesnocancel(
                "File exists",
                "File exists. Yes=overwrite, No=append, Cancel=cancel.",
            )
            if choice is None:
                return
            if choice is False:
                mode = "a"

        with open(path, mode, encoding="utf-8") as f:
            if mode == "a":
                f.write("\n\n")
            f.write(content)

    def _open_about(self) -> None:
        AboutDialog(self.parent)
