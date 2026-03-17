#region usings

using System;
using System.Colletions.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TLIClib;
using System.IO;
using System.Xml;
using System.Configuration;
using System.Reflection;

#endregion

namespace TLICs
{
    public partial class MainForm : Form
    {
        #region Data Members

        // enumerated combobox values for KVs and temperatures:
        private static double[] kvs = { 0.48, 0.6, 2.4, 4.0, 8.0, 12.0, 13.0, 13.8, 23.0, 33.0, 46.0, 69.0, 115.0, 230.0, 500.0 };
        private static double[] temps = { 26.7, 40.0 };

        // string to hold output data:
        public string impedances = "";

        // instance of dataset containing schema for line section table:
        private DataSet1 branchDataset = new DataSet1();

        // string values:
        private string condFile;
        private string structFile;
        private string condName;
        private string staticName;
        private string fullStructName;
        private string structName;
        private string isCustomStructure;

        // double values:
        private double mileage;
        private double rho;
        private double kv;
        private double temp;
        private double maxOpTemp;
        private double mva;
        private double mvaRateA = 0;
        private double mvaRateB = 0;
        private double mvaRateC = 0;
        private double secondMvaRateA = 0;
        private double secondMvaRateB = 0;
        private double secondMvaRateC = 0;
        private double currentMvaRateA = 0;
        private double currentMvaRateB = 0;
        private double currentMvaRateC = 0;

        // default values:
        private static double def_mile = 1.0;
        private static double def_rho = 100.0;
        private static double def_kv = 230.0;
        private static double def_temp = 40.0;
        private static double def_mva = 100.0;

        // lists to be populated from files and loaded into comboboxes:
        private List<string> condNames;
        private List<string> staticNames;
        private List<string> structNames;
        private List<string> structSES;

        // master branch object populated at CalculateImpedances:
        private Branch_JWR branch = null;

        // cache current structure object:
        private Structure_JWR structure;

        private bool externalDataCond = false;
        private bool externalDataStruct = false;

        private Assembly assembly;
        private TextReader reader;

        private LineRatingCalc LineRate = new LineRatingCalc();

        #endregion

        #region Constructor

        public MainForm()
        {
            //run microsoft form designer auto-generated code:
            InitializeComponent();

            LineRate.InitializeCalculator();

            //construct branch object with default values:
            branch = new Branch_JWR(Convert.ToInt32(kv), 1, 2, "1", 1);

            //initialize lists:
            condNames = new List<string>();
            staticNames = new List<string>();
            structNames = new List<string>();
            structSES = new List<string>();

            //set combobox datasources and default selected indices:
            cmbTemp.DataSource = temps;
            cmbTemp.SelectedIndex = 1;
            cmbKVBase.DataSource = kvs;
            cmbKVBase.SelectedIndex = 12;
            tbImpedances.Text = "-No Data-";

            //set branch settings to default values:
            mileage = def_mile;
            rho = def_rho;
            kv = def_kv;
            temp = def_temp;
            mva = def_mva;

            //init custom structure flag:
            isCustomStructure = "False";

            //try to find the data files in their default location:
            try
            {
                tbCondFile.Text = "(Using default data. Hover over to see location)"; //force to revert to content file
                ToolTip tbCondFileToolTip = new ToolTip();
                tbCondFileToolTip.ShowAlways = true;
                tbCondFileToolTip.SetToolTip(tbCondFile,
                    @"G:\LTR_General\SOFTWARE\_IN HOUSE\TLiCS\Source\TLiCS\Resources\conddata.csv");
            }
            catch (Exception ex)
            {
                //do nothing
                MessageBox.Show("Exception: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            try
            {
                tbStructFile.Text = "(Using default data. Hover over to see location)"; //force to revert to content file
                ToolTip tbStructFileToolTip = new ToolTip();
                tbStructFileToolTip.ShowAlways = true;
                tbStructFileToolTip.SetToolTip(tbStructFile,
                    @"G:\LTR_General\SOFTWARE\_IN HOUSE\TLiCS\Source\TLiCS\Resources\structdata.txt");
            }
            catch (Exception)
            {
                //do nothing
            }

            //try to set structure/conductor defaults:
            try
            {
                cmbCondName.Text = "1272 ACSR (BITTERN)";
                cmbStaticName.Text = "1/4 GALV (None)";
                cmbStructureName.Text = "BPV";

                //LoadStructureList();
                //LoadCondList();
            }
            catch (Exception)
            {
                //do nothing
            }
        }

        #endregion

        #region Data File Management

        private void OnBrowseCond(object sender, EventArgs e)
        {
            //show open file dialog and get dialog result:
            openFileDiag.InitialDirectory = @"G:\LTR_General\SOFTWARE\_IN HOUSE\TLiCS\Source\TLiCS\Resources";
            openFileDiag.RestoreDirectory = true;
            DialogResult result = openFileDiag.ShowDialog();

            if (result == DialogResult.OK) //if OK button clicked:
            {
                //set flag for external cond file:
                externalDataCond = true;

                //load file variable from dialog:
                condFile = openFileDiag.FileName;

                //load textbox from variable:
                tbCondFile.Text = condFile;
            }
        }

        private void OnBrowseStruct(object sender, EventArgs e)
        {
            //show open file dialog and get dialog result:
            openFileDiag.InitialDirectory = @"G:\LTR_General\SOFTWARE\_IN HOUSE\TLiCS\Source\TLiCS\Resources";
            openFileDiag.RestoreDirectory = true;
            DialogResult result = openFileDiag.ShowDialog();

            if (result == DialogResult.OK)
            {
                //set flag for external structure file:
                externalDataStruct = true;

                //load file variable from dialog:
                structFile = openFileDiag.FileName;

                //load textbox from variable:
                tbStructFile.Text = structFile;

                //reload combo box:
                LoadStructureList();
            }
        }

        private void OnCondFileChange(object sender, EventArgs e)
        {
            LoadCondList();
        }

        private void LoadCondList()
        {
            try
            {
                if (externalDataCond)
                {
                    condNames = new List<string>();
                    staticNames = new List<string>();

                    condFile = tbCondFile.Text;

                    reader = new StreamReader(condFile);
                }
                else
                {
                    tbCondFile.Text = @"(Using default data. Hover over to see location)"; //force to revert to embedded file

                    condNames = new List<string>();
                    staticNames = new List<string>();

                    condFile = tbCondFile.Text;

                    assembly = Assembly.GetExecutingAssembly();
                    //reader = new StreamReader(assembly.GetManifestResourceStream("TLiCS.Resources.conddata.csv"));
                    reader = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");
                }

                string line = reader.ReadLine();
                line = reader.ReadLine();

                string[] fields;

                while (line != null)
                {
                    fields = line.Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length < 2)
                    {
                        fields = line.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    if (fields[0].Equals("1"))
                    {
                        staticNames.Add(fields[12]);   //add this line's conductor data to static lists
                    }
                    else
                    {
                        condNames.Add(fields[12]);     //add this line's conductor data to conductor lists
                    }

                    line = reader.ReadLine(); //read next line
                }

                string[] condCopy = new string[condNames.Count];
                condNames.CopyTo(condCopy);

                string[] staticCopy = new string[staticNames.Count];
                staticNames.CopyTo(staticCopy);

                staticNames.Add("----------");
                condNames.Add("----------");

                foreach (string cond in condCopy)
                {
                    staticNames.Add(cond);
                }

                foreach (string stat in staticCopy)
                {
                    condNames.Add(stat);
                }

                //load comboboxes with updated lists:
                cmbCondName.DataSource = condNames;
                cmbStaticName.DataSource = staticNames;

                //in case the comboboxes were previously unloaded, we need to activate the line section add button:
                ActivateAdd();

                //calculate impedances based on (possibly) new conductor data:
                CalculateImpedances(true);
            }
            catch (Exception ex)
            {
                //do nothing
                MessageBox.Show("Exception: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnStructFileChange(object sender, EventArgs e)
        {
            LoadStructureList();
        }

        private void LoadStructureList()
        {
            try
            {
                //load custom structures if any:
                if (branchDataset.CustomStructure.Rows.Count > 0)
                {
                    foreach (DataSet1.CustomStructureRow row in branchDataset.CustomStructure.Rows)
                    {
                        structNames.Add(row.Name);
                    }
                }

                //load names from file:
                structFile = tbStructFile.Text;

                if (externalDataStruct)
                {
                    reader = new StreamReader(structFile);
                }
                else
                {
                    assembly = Assembly.GetExecutingAssembly();
                    //reader = new StreamReader(assembly.GetManifestResourceStream("TLiCS.Resources.structdata.txt"));
                    reader = new StreamReader(Application.StartupPath + @"\Resources\structdata.txt");
                }

                string line = reader.ReadLine();
                line = reader.ReadLine();
                string[] lines = null;
                Structure_JWR dummy = null;

                structES.Clear();
                structNames.Clear();

                while (line != null)
                {
                    lines = line.Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
                    structNames.Add(lines[0]);

                    double[] x = new double[5];
                    double[] y = new double[5];

                    x[0] = Convert.ToDouble(lines[1]);
                    x[1] = Convert.ToDouble(lines[3]);
                    x[2] = Convert.ToDouble(lines[5]);

                    y[0] = Convert.ToDouble(lines[2]);
                    y[1] = Convert.ToDouble(lines[4]);
                    y[2] = Convert.ToDouble(lines[6]);

                    dummy = new Structure_JWR("", x, y);

                    structES.Add(dummy.ES.ToString("F2"));

                    line = reader.ReadLine();
                }

                string[] structData = new string[structNames.Count];

                for (int i = 0; i < structData.Length; i++)
                {
                    string space = "";
                    for (int j = 11; j > structNames[i].Length; j--)
                    {
                        space = space + " ";
                    }

                    structData[i] = structNames[i] + space + "(" + structES[i] + ")";
                }

                cmbStructureName.DataSource = structData;
                Properties.Settings.Default.lastStructFile = structFile;
                ActivateAdd();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception while loading structures from file: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            CalculateImpedances(true);
        }

        #endregion

        #region Input Data

        private void OnCondChange(object sender, EventArgs e)
        {
            OnCondOrSeasonChange();
        }

        private void OnSeasonChange(object sender, EventArgs e)
        {
            if (branchDataset.LineSection.Count != 0)
            {
                MessageBox.Show("You may want to Clear All Sections before switching between summer and winter ratings.");
            }

            cmbTemp.SelectedIndex = 1;
            OnCondOrSeasonChange();
        }

        private void OnWinSeason(object sender, EventArgs e)
        {
            cmbTemp.SelectedIndex = 0;
            OnCondOrSeasonChange();
        }

        private void OnCondOrSeasonChange()
        {
            condName = cmbCondName.Text.Trim().ToLower();
            string condSolveMsg = "";

            if (externalDataCond)
            {
                reader = new StreamReader(condFile);
            }
            else
            {
                assembly = Assembly.GetExecutingAssembly();
                //reader = new StreamReader(assembly.GetManifestResourceStream("TLiCS.Resources.conddata.csv"));
                reader = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");
            }

            Conductor_JWR cond = new Conductor_JWR(condName, reader);

            tbCondDesc.Text = "";
            tbCondDesc.AppendText("gmr:\t" + cond.GMR.ToString("F4") + " ft\n");
            tbCondDesc.AppendText("rad:\t" + cond.Radius.ToString("F4") + " ft\n");
            tbCondDesc.AppendText("R:\t" + cond.R.ToString("F4") + " ohm/mi\n");
            tbCondDesc.AppendText("XL:\t" + cond.XL.ToString("F4") + " ohm/mi\n");
            tbCondDesc.AppendText("XC:\t" + cond.XC.ToString("F4") + " Mohm-mi\n");

            condSolveMsg = LineRate.selectConductorSolve(
                radioButtonSummer.Checked,
                condName,
                Convert.ToDouble(cmbTemp.Text),
                Convert.ToDouble(textBoxMOT.Text));

            if (condSolveMsg != "")
            {
                tbCondDesc.AppendText(condSolveMsg + "\n");
                tbCondDesc.AppendText("Rating A:\t" + cond.RateA + " A\n");
                tbCondDesc.AppendText("Rating B:\t" + cond.RateB + " A\n");
                tbCondDesc.AppendText("Rating C:\t" + cond.RateC + " A\n");

                mvaRateA = cond.RateA;
                mvaRateB = cond.RateB;
                mvaRateC = cond.RateC;
            }
            else
            {
                tbCondDesc.AppendText("Sum Rating A:\t" + Convert.ToInt16(LineRate.rateA) + " A\n");
                tbCondDesc.AppendText("Sum Rating B:\t" + Convert.ToInt16(LineRate.rateB) + " A\n");
                tbCondDesc.AppendText("Sum Rating C:\t" + Convert.ToInt16(LineRate.rateC) + " A\n");

                if (radioButtonSummer.Checked)
                {
                    mvaRateA = LineRate.rateA;
                    mvaRateB = LineRate.rateB;
                    mvaRateC = LineRate.rateC;
                }
            }

            condSolveMsg = LineRate.selectConductorSolve(
                radioButtonSummer.Checked,
                condName,
                Convert.ToDouble(cmbTemp.Text),
                Convert.ToDouble(textBoxMOT.Text));

            if (condSolveMsg == "")
            {
                tbCondDesc.AppendText("Win Rating A:\t" + Convert.ToInt16(LineRate.rateA) + " A\n");
                tbCondDesc.AppendText("Win Rating B:\t" + Convert.ToInt16(LineRate.rateB) + " A\n");
                tbCondDesc.AppendText("Win Rating C:\t" + Convert.ToInt16(LineRate.rateC) + " A\n");

                if (!radioButtonSummer.Checked)
                {
                    mvaRateA = LineRate.rateA;
                    mvaRateB = LineRate.rateB;
                    mvaRateC = LineRate.rateC;
                }
            }

            if (!radioButtonSummer.Checked)
            {
                mvaRateA = LineRate.rateA;
                mvaRateB = LineRate.rateB;
                mvaRateC = LineRate.rateC;
            }

            StaticWorker();
        }

        private void OnStaticChange(object sender, EventArgs e)
        {
            StaticWorker();
        }

        private void StaticWorker()
        {
            staticName = cmbStaticName.Text.Trim();

            if (externalDataCond)
            {
                reader = new StreamReader(condFile);
            }
            else
            {
                assembly = Assembly.GetExecutingAssembly();
                //reader = new StreamReader(assembly.GetManifestResourceStream("TLiCS.Resources.conddata.csv"));
                reader = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");
            }

            Conductor_JWR cond = new Conductor_JWR(staticName, reader);

            tbStaticDesc.Text = "";
            tbStaticDesc.AppendText("gmr:\t" + cond.GMR.ToString("F4") + " ft\n");
            tbStaticDesc.AppendText("rad:\t" + cond.Radius.ToString("F4") + " ft\n");
            tbStaticDesc.AppendText("R:\t" + cond.R.ToString("F4") + " ohm/mi\n");
            tbStaticDesc.AppendText("XL:\t" + cond.XL.ToString("F4") + " ohm/mi\n");
            tbStaticDesc.AppendText("XC:\t" + cond.XC.ToString("F4") + " Mohm-mi\n");
            tbStaticDesc.AppendText("Amp A:\t" + cond.RateA.ToString() + " A\n");
            tbStaticDesc.AppendText("Amp B:\t" + cond.RateB.ToString() + " A\n");
            tbStaticDesc.AppendText("Amp C:\t" + cond.RateC.ToString() + " A\n");
        }

        private void OnStructureChange(object sender, EventArgs e)
        {
            //get structure name from textbox:
            fullStructName = cmbStructureName.Text.Trim();
            string[] fields = fullStructName.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            structName = fields[0];

            bool inDB = false;

            if (externalDataStruct)
            {
                reader = new StreamReader(structFile);
            }
            else
            {
                assembly = Assembly.GetExecutingAssembly();
                //reader = new StreamReader(assembly.GetManifestResourceStream("TLiCS.Resources.structdata.txt"));
                reader = new StreamReader(Application.StartupPath + @"\Resources\structdata.txt");
            }

            //first see if structure is in database:
            try
            {
                if (branchDataset.CustomStructure.Rows.Count > 0)
                {
                    foreach (DataSet1.CustomStructureRow row in branchDataset.CustomStructure.Rows)
                    {
                        if (row.Name.Equals(structName))
                        {
                            inDB = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unknown exception while loading structure data from database. \n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            if (!inDB)
            {
                //load structure definition from file:
                try
                {
                    this.structure = new Structure_JWR(structName, reader);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Structure " + structName + " not found in file. \n" + ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            DrawStructure();
        }

        private void OnMileageChange(object sender, EventArgs e)
        {
            // do nothing
        }

        private void tbMileage_Leave(object sender, EventArgs e)
        {
            try
            {
                tbFeet.Text = Convert.ToString(Convert.ToDouble(tbMileage.Text) * 5280);
            }
            catch
            {
                // Do nothing
            }
        }

        private void tbMileage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                try
                {
                    tbFeet.Text = Convert.ToString(Convert.ToDouble(tbMileage.Text) * 5280);
                }
                catch
                {
                    // Do nothing
                }
            }
        }

        private void OnFeetChange(object sender, EventArgs e)
        {
            // do nothing
        }

        private void tbFeet_Leave(object sender, EventArgs e)
        {
            try
            {
                tbMileage.Text = Convert.ToString(Convert.ToDouble(tbFeet.Text) / 5280);
            }
            catch
            {
                // Do nothing
            }
        }

        private void tbFeet_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                try
                {
                    tbMileage.Text = Convert.ToString(Convert.ToDouble(tbFeet.Text) / 5280);
                }
                catch
                {
                    // Do nothing
                }
            }
        }

        private void tbFeet_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Do nothing
        }

        #endregion

        #region Branch_JWR Options

        private void OnMVAChange(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnKVChange(object sender, EventArgs e)
        {
            if (cmbKVBase.Text == "500")
            {
                cmbKVBase.ForeColor = Color.Blue;
            }
            else if (cmbKVBase.Text == "230")
            {
                cmbKVBase.ForeColor = Color.Red;
            }
            else if (cmbKVBase.Text == "115")
            {
                cmbKVBase.ForeColor = Color.Green;
            }
            else if (cmbKVBase.Text == "46")
            {
                cmbKVBase.ForeColor = Color.Purple;
            }
            else if (cmbKVBase.Text == "33")
            {
                cmbKVBase.ForeColor = Color.Pink;
            }
            else
            {
                cmbKVBase.ForeColor = Color.Black;
            }

            CalculateImpedances(true);
        }

        private void OnRhoChange(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnTempChange(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnBus1Change(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnBus2Change(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnCktChange(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        private void OnInServiceChange(object sender, EventArgs e)
        {
            CalculateImpedances(true);
        }

        #endregion

        #region Line Sections

        private void ActivateAdd()
        {
            btnAddSection.Enabled = true;
        }

        private void OnClearSections(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to clear all recorded line sections?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                currentMVARateA = 0;
                currentMVARateB = 0;
                currentMVARateC = 0;
                branchDataset.LineSection.Clear();
                dataGridSections.DataSource = branchDataset.LineSection;
            }

            CalculateImpedances(false);
        }

        private void OnAddSection(object sender, EventArgs e)
        {
            try
            {
                isCustomStructure = "False";

                foreach (DataSet1.CustomStructureRow row in branchDataset.CustomStructure.Rows)
                {
                    if (row.Name.Equals(structure.Name))
                    {
                        isCustomStructure = "True";
                    }
                }

                mileage = Convert.ToDouble(tbMileage.Text);
                maxOpTemp = Convert.ToDouble(textBoxMOT.Text);

                branchDataset.LineSection.AddLineSectionRow(
                    condName,
                    staticName,
                    structName,
                    mileage,
                    isCustomStructure,
                    maxOpTemp);

                dataGridSections.DataSource = branchDataset.LineSection;

                CalculateImpedances(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(("Unknown Exception: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnLineSectionChange(object sender, DataGridViewCellEventArgs e)
        {
            dataGridSections.DataSource = branchDataset.LineSection;
            CalculateImpedances(true);
        }

        #endregion

        #region Output

        private bool CalculateImpedances(bool addBranch)
        {
            bool success = false;

            if (branchDataset.LineSection.Rows.Count > 0) //if line sections exist:
            {
                //declare objects:
                TLine_JWR sec;
                Structure_JWR str;
                Conductor_JWR ph;
                Conductor_JWR st;

                try
                {
                    //load data from text boxes:
                    mva = Convert.ToDouble(tbMVABase.Text);
                    temp = Convert.ToDouble(cmbTemp.Text);
                    rho = Convert.ToDouble(tbRho.Text);
                    kv = Convert.ToDouble(cmbKVBase.Text);

                    //grab branch status flag from check box:
                    int status = 0;
                    if (cbxStatus.Checked)
                    {
                        status = 1;
                    }

                    if (tbBus1.Text.Equals(string.Empty))
                    {
                        tbBus1.Text = "1";
                    }

                    if (tbBus2.Text.Equals(string.Empty))
                    {
                        tbBus2.Text = "2";
                    }

                    if (tbCkt.Text.Equals(string.Empty))
                    {
                        tbCkt.Text = "1";
                    }

                    //Build the new branch:
                    branch = new Branch_JWR(
                        Convert.ToInt32(Math.Ceiling(kv)),
                        Convert.ToInt32(tbBus1.Text),
                        Convert.ToInt32(tbBus2.Text),
                        tbCkt.Text,
                        status);

                    //set default value for string output:
                    bool empty = true;

                    impedances = "Impedance Calculation Output:\n\n";
                    impedances += "Struct    Conductor              Static              MOT    Mileage\n";
                    impedances += "---------------------------------------------------------------------\n";

                    TextReader structReader = null;
                    TextReader condReader1 = null;
                    TextReader condReader2 = null;

                    //Add line sections to branch object:
                    foreach (DataSet1.LineSectionRow row in branchDataset.LineSection)
                    {
                        empty = false;

                        assembly = Assembly.GetExecutingAssembly();
                        structReader = new StreamReader(Application.StartupPath + @"\Resources\structdata.txt");

                        condReader1 = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");
                        condReader2 = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");

                        if (externalDataStruct)
                        {
                            structReader = new StreamReader(structFile);
                        }

                        if (externalDataCond)
                        {
                            condReader1 = new StreamReader(condFile);
                            condReader2 = new StreamReader(condFile);
                        }

                        str = new Structure_JWR(row.StructName, structReader);
                        structReader.Close();

                        ph = new Conductor_JWR(row.CondName, condReader1);
                        condReader1.Close();

                        st = new Conductor_JWR(row.StaticName, condReader2);
                        condReader2.Close();

                        mileage = row.Mileage;
                        sec = new TLine_JWR(str, ph, st, mileage, rho);
                        branch.AddLineSection(sec);

                        string tmp;
                        tmp = String.Format("{0,-9}", row.StructName);
                        impedances += tmp;
                        tmp = String.Format("{0,-27}", row.CondName);
                        impedances += tmp;
                        tmp = String.Format("{0,-25}", row.StaticName);
                        impedances += tmp;
                        tmp = String.Format("{0,-7}", row.MOT);
                        impedances += tmp;
                        impedances += mileage.ToString("F2") + "\n";
                    }

                    if (!empty) //if at least one line section in branch
                    {
                        OnCondOrSeasonChange();

                        if (addBranch)
                        {
                            currentMVARateA = 0;
                            currentMVARateB = 0;
                            currentMVARateC = 0;
                        }

                        //determine MVA rating from selected conductor or from already-populated branch if its rating is lower
                        foreach (DataSet1.LineSectionRow row in branchDataset.LineSection)
                        {
                            condName = row.CondName.ToLower();
                            string condSolveMsg = "";

                            if (externalDataCond)
                            {
                                reader = new StreamReader(condFile);
                            }
                            else
                            {
                                assembly = Assembly.GetExecutingAssembly();
                                reader = new StreamReader(Application.StartupPath + @"\Resources\conddata.csv");
                            }

                            Conductor_JWR cond = new Conductor_JWR(condName, reader);

                            maxOpTemp = row.MOT;

                            condSolveMsg = LineRate.selectConductorSolve(
                                radioButtonSummer.Checked,
                                condName,
                                temp,
                                maxOpTemp);

                            if (condSolveMsg != "")
                            {
                                secondMVARateA = cond.RateA;
                                secondMVARateB = cond.RateB;
                                secondMVARateC = cond.RateC;
                            }
                            else
                            {
                                if (radioButtonSummer.Checked)
                                {
                                    secondMVARateA = LineRate.rateA;
                                    secondMVARateB = LineRate.rateB;
                                    secondMVARateC = LineRate.rateC;
                                }
                            }

                            condSolveMsg = LineRate.selectConductorSolve(
                                radioButtonSummer.Checked,
                                condName,
                                temp,
                                maxOpTemp);

                            if (condSolveMsg != "")
                            {
                                if (radioButtonSummer.Checked)
                                {
                                    secondMVARateA = LineRate.rateA;
                                    secondMVARateB = LineRate.rateB;
                                    secondMVARateC = LineRate.rateC;
                                }
                            }

                            if (!radioButtonSummer.Checked)
                            {
                                secondMVARateA = LineRate.rateA;
                                secondMVARateB = LineRate.rateB;
                                secondMVARateC = LineRate.rateC;
                            }

                            if (currentMVARateA > secondMVARateA || currentMVARateA == 0)
                                currentMVARateA = secondMVARateA;
                            if (currentMVARateB > secondMVARateB || currentMVARateB == 0)
                                currentMVARateB = secondMVARateB;
                            if (currentMVARateC > secondMVARateC || currentMVARateC == 0)
                                currentMVARateC = secondMVARateC;
                        }

                        //populate output textbox string:
                        impedances += "\n";
                        impedances += "Total Length:\t\t" + branch.Length.ToString("F2") + " mi\n";
                        impedances += "Rating A:\t\t" + Math.Round(currentMVARateA * Math.Sqrt(3) * branch.KV / 1000, 2) + " MVA\n";
                        impedances += "Rating B:\t\t" + Math.Round(currentMVARateB * Math.Sqrt(3) * branch.KV / 1000, 2) + " MVA\n";
                        impedances += "Rating C:\t\t" + Math.Round(currentMVARateC * Math.Sqrt(3) * branch.KV / 1000, 2) + " MVA\n";
                        impedances += "Nominal Voltage:\t" + branch.KV + " kV\n";
                        impedances += "MVA Base:\t\t100.0 MVA\n";
                        impedances += "\n";

                        impedances += "Per Unit Positive Sequence Impedances:\n";
                        impedances += "----------------------------------------\n";
                        impedances += "R: " + branch.R1.ToString("F6") + " p.u.\n";
                        impedances += "X: " + branch.X1.ToString("F6") + " p.u.\n";
                        impedances += "B: " + branch.C1.ToString("F6") + " p.u.\n";
                        impedances += "\n";

                        impedances += "Per Unit Zero Sequence Impedances:\n";
                        impedances += "-----------------------------------\n";
                        impedances += "R0: " + branch.R0.ToString("F6") + " p.u.\n";
                        impedances += "X0: " + branch.X0.ToString("F6") + " p.u.\n";
                        impedances += "B0: " + branch.C0.ToString("F6") + " p.u.\n";
                        impedances += "\n";

                        impedances += "Per Mile Impedances:\n";
                        impedances += "-----------------------------------\n";

                        if (branch.LineSections.Length == 1) //if one line section only
                        {
                            impedances += "Z1: " + branch.Impedances[6].ToString("F6") + " + j" +
                                          branch.Impedances[7].ToString("F6") + " ohm/mi\n";
                            impedances += "Y1: " + "0.000000" + " + j" +
                                          branch.Impedances[8].ToString("F6") + " us/mi\n";
                            impedances += "\n";
                            impedances += "Z0: " + branch.Impedances[9].ToString("F6") + " + j" +
                                          branch.Impedances[10].ToString("F6") + " ohm/mi\n";
                            impedances += "Y0: " + "0.000000" + " + j" +
                                          branch.Impedances[11].ToString("F6") + " us/mi\n";
                            impedances += "\n";
                        }
                        else //per mile impedances are only applicable to a single section
                        {
                            impedances += "(per mile impedances are not applicable to multi-section branches)\n";
                            impedances += "\n";
                        }

                        impedances += "PSS/E Format:\n";
                        impedances += "------------------------------\n";
                        impedances += "raw string:\n" + branch.RawString + " / " + tbBus1Name.Text + " " + tbBus2Name.Text + "\n";
                        impedances += "\n";
                        impedances += "seq string:\n" + branch.SeqString + "\n";
                        impedances += "\n";

                        success = true;
                    }
                    else
                    {
                        impedances = "No Data";
                    }

                    //load textbox with output string:
                    tbImpedances.Text = impedances;
                }
                catch (FormatException ex)
                {
                    MessageBox.Show("Number Format Exception: " + ex.Message,
                        "Number Format Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    tbMVABase.Text = Convert.ToString(mva);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Exception while calculating Impedances: " + ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    tbBus2.Text = "2";
                }
            }

            return success;
        }

        private void OnHelpClick(object sender, EventArgs e)
        {
            AboutBox box = new AboutBox();
            box.ShowDialog();
        }

        private void DrawStructure()
        {
            //declare and init a few variables:
            double xmin = 0.0;
            double ymax = 0.0;
            bool stat = false;
            NPlot.LabelPointPlot plot2 = new NPlot.LabelPointPlot();

            //clear plot surface:
            plotSurfaceStructure.Clear();

            NPlot.LabelPointPlot plot = new NPlot.LabelPointPlot();
            plot.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 6, new Pen(Color.Red), true);
            plot.AbscissaData = new double[] { structure.A[0].X, structure.A[1].X, structure.A[2].X };
            plot.OrdinateData = new double[] { structure.A[0].Y, structure.A[1].Y, structure.A[2].Y };
            plot.TextData = new string[] { "A", "B", "C" };
            xmin = plot.SuggestXAxis().WorldMin;
            ymax = plot.SuggestYAxis().WorldMax;

            //Draw static(s) if present:
            if (structure.StaticCount == 1)
            {
                stat = true;
                plot2.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 4, new Pen(Color.Blue), true);
                plot2.AbscissaData = new double[] { structure.G[0].X };
                plot2.OrdinateData = new double[] { structure.G[0].Y };
                plot2.TextData = new string[] { "G" };
                ymax = plot2.SuggestYAxis().WorldMax;
            }

            if (structure.StaticCount == 2)
            {
                stat = true;
                plot2.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 4, new Pen(Color.Blue), true);
                plot2.AbscissaData = new double[] { structure.G[0].X, structure.G[1].X };
                plot2.OrdinateData = new double[] { structure.G[0].Y, structure.G[1].Y };
                plot2.TextData = new string[] { "G1", "G2" };
                ymax = plot2.SuggestYAxis().WorldMax;
            }

            //Draw center line representing pole:
            NPlot.LinePlot pole = new NPlot.LinePlot(new double[] { 0.0, ymax }, new double[] { 0.0, 0.0 });
            pole.Pen = new Pen(Color.LightGray, 2.0F);
            plotSurfaceStructure.Add(pole);

            plotSurfaceStructure.Add(plot);
            if (stat) //if static wires present
            {
                plotSurfaceStructure.Add(plot2);
            }

            //Set Bounds:
            plotSurfaceStructure.YAxis1.WorldMin = 0.0;
            plotSurfaceStructure.YAxis1.WorldMax = ymax + 10;
            plotSurfaceStructure.XAxis1.WorldMin = -plotSurfaceStructure.YAxis1.WorldMax * 0.5 * 0.8;
            plotSurfaceStructure.XAxis1.WorldMax = plotSurfaceStructure.YAxis1.WorldMax * 0.5 * 0.8;

            //Force Redraw:
            plotSurfaceStructure.Refresh();
        }

        #endregion

        #region File Management

        private void OnSave(object sender, EventArgs e)
        {
            saveFileDiag = new SaveFileDialog();
            string filename = string.Empty;
            saveFileDiag.AddExtension = true;
            saveFileDiag.DefaultExt = "xml";
            saveFileDiag.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            DialogResult result = saveFileDiag.ShowDialog();

            if (result == DialogResult.OK)
            {
                filename = saveFileDiag.FileName;
                try
                {
                    branchDataset.BranchOptions.Clear();
                    DataSet1.BranchOptionsRow row = branchDataset.BranchOptions.NewBranchOptionsRow();
                    row.Bus1 = Convert.ToInt32(tbBus1.Text);
                    row.Bus2 = Convert.ToInt32(tbBus2.Text);
                    row.Ckt = tbCkt.Text;
                    row.InService = cbxStatus.Checked;
                    row.KV = kv;
                    row.MVA = mva;
                    row.Temp = temp;
                    row.Rho = rho;
                    branchDataset.BranchOptions.AddBranchOptionsRow(row);
                    branchDataset.WriteXml(filename);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(("Unknown exception while saving file: " + ex.Message),
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnOpen(object sender, EventArgs e)
        {
            openFileDiag = new OpenFileDialog();
            string filename = string.Empty;
            openFileDiag.DefaultExt = "xml";
            openFileDiag.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            DialogResult result = openFileDiag.ShowDialog();

            if (result == DialogResult.OK)
            {
                filename = openFileDiag.FileName;
                try
                {
                    branchDataset.Clear();
                    branchDataset.ReadXml(filename);

                    //branch options:
                    tbBus1.Text = branchDataset.BranchOptions[0].Bus1.ToString();
                    tbBus2.Text = branchDataset.BranchOptions[0].Bus2.ToString();
                    tbCkt.Text = branchDataset.BranchOptions[0].Ckt;
                    cbxStatus.Checked = branchDataset.BranchOptions[0].InService;
                    cmbKVBase.Text = branchDataset.BranchOptions[0].KV.ToString("F2");
                    kv = branchDataset.BranchOptions[0].KV;
                    tbMVABase.Text = branchDataset.BranchOptions[0].MVA.ToString("F0");
                    cmbTemp.Text = branchDataset.BranchOptions[0].Temp.ToString("F2");
                    temp = branchDataset.BranchOptions[0].Temp;
                    rho = branchDataset.BranchOptions[0].Rho;
                    tbRho.Text = branchDataset.BranchOptions[0].Rho.ToString("F2");

                    //dump line section data into line section datagrid:
                    dataGridSections.DataSource = branchDataset.LineSection;
                    dataGridSections.Refresh();

                    //just to make sure:
                    CalculateImpedances(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(("Unknown exception while opening file: " + ex.Message),
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to close the current branch file?\n" +
                "(This will reset all values to their defaults)",
                "Close Branch File Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Exclamation,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                branchDataset.Clear();
                OnCondFileChange(sender, e);
                OnStructFileChange(sender, e);
                tbBus1.Text = "1";
                tbBus2.Text = "2";
                tbCkt.Text = "1";
                tbMileage.Text = "1.0";
                tbMVABase.Text = "100";
                tbRho.Text = "100";
                cmbKVBase.Text = "230";
                cbxStatus.Checked = true;
            }
        }

        private void OnBuildPython(object sender, EventArgs e)
        {
            ScriptWriter("py");
        }

        private void OnBuildAux(object sender, EventArgs e)
        {
            ScriptWriter("aux");
        }

        private void ScriptWriter(string scriptType)
        {
            if (CalculateImpedances(true)) // only build if CalculateImpedances() does not fail
            {
                TextWriter writer = null;

                bool includeSequence = false;
                if (radPythonSeq.Checked)
                {
                    includeSequence = true;
                }

                bool append = false;
                string comm = "# ";

                saveFileDiag = new SaveFileDialog();
                saveFileDiag.AddExtension = true;
                saveFileDiag.DefaultExt = scriptType;

                if (scriptType == "py")
                {
                    saveFileDiag.Filter = "Python Script (*.py)|*.py|All files (*.*)|*.*";
                    saveFileDiag.Title = "Build Python File";
                }
                else
                {
                    saveFileDiag.Filter = "PowerWorld Auxiliary Files (*.aux)|*.aux|All files (*.*)|*.*";
                    saveFileDiag.Title = "Build Aux File";
                    comm = "// ";
                }

                saveFileDiag.OverwritePrompt = false;
                DialogResult result = saveFileDiag.ShowDialog();

                if (result == DialogResult.OK)
                {
                    string path = saveFileDiag.FileName;

                    try // we will catch all exceptions
                    {
                        DialogResult overwrite = DialogResult.Cancel;

                        if (File.Exists(path))
                        {
                            overwrite = MessageBox.Show(
                                "File exists!! Press Yes to overwrite, No to append, Cancel to cancel.",
                                "File exists",
                                MessageBoxButtons.YesNoCancel);

                            if (overwrite == DialogResult.No)
                            {
                                append = true;
                            }
                        }

                        if (overwrite != DialogResult.Cancel)
                        {
                            if (append)
                            {
                                writer = File.AppendText(path);
                                writer.WriteLine();
                                writer.WriteLine();
                            }
                            else
                            {
                                writer = new StreamWriter(path);
                                string[] pathParts = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                                string filename = pathParts[pathParts.Length - 1];

                                if (scriptType == "py")
                                {
                                    writer.WriteLine("# python automation script");
                                }
                                else
                                {
                                    writer.WriteLine("// AUX automation script");
                                }

                                writer.WriteLine(comm + "file: [" + filename + "]");
                                writer.WriteLine(comm + "auto generated by TLICS program");
                                writer.WriteLine();
                            }

                            writer.WriteLine(comm + tbLineName.Text + " line params:");

                            if (scriptType == "py") // py bus nums, names, and ckt ID
                            {
                                writer.WriteLine("bus1 = " + Convert.ToInt32(tbBus1.Text) + " # " + tbBus1Name.Text);
                                writer.WriteLine("bus2 = " + Convert.ToInt32(tbBus2.Text) + " # " + tbBus2Name.Text);
                                writer.WriteLine("ckt = '" + tbCkt.Text + "'");
                            }
                            else
                            {
                                writer.WriteLine("DATA (Branch, [BusNum,BusNum:1,LineCircuit,LineStatus,LineLength,BranchDeviceType,LineXfmr,");
                                writer.WriteLine("LineR,LineX,LineC,LineMVA,LineMVA:1,LineMVA:2], YES) //, BusName,BusName:1) // aux DATA line contd.");
                                writer.WriteLine("{");
                                writer.Write("\t" + Convert.ToInt32(tbBus1.Text) + " " +
                                             Convert.ToInt32(tbBus2.Text) + " \"" +
                                             tbCkt.Text + "\" ");

                                if (cbxStatus.Checked) // aux status closed
                                {
                                    writer.Write("\"Closed\" ");
                                }
                                else // aux status open
                                {
                                    writer.Write("\"Open\" ");
                                }

                                writer.Write(branch.Length + " \"Line\" \"NO\" ");
                            }

                            if (scriptType == "py" & cbxStatus.Checked) // py status closed
                            {
                                writer.WriteLine("status = 1");
                            }
                            else if (scriptType == "py") // py status open
                            {
                                writer.WriteLine("status = 0");
                            }

                            if (scriptType == "py") // py impedances
                            {
                                writer.WriteLine("r1 = " + branch.R1.ToString("F6"));
                                writer.WriteLine("x1 = " + branch.X1.ToString("F6"));
                                writer.WriteLine("b1 = " + branch.C1.ToString("F6"));
                            }
                            else // aux impedances
                            {
                                writer.Write(branch.R1.ToString("F6") + " " +
                                             branch.X1.ToString("F6") + " " +
                                             branch.C1.ToString("F6") + " ");
                            }

                            if (scriptType == "py" & includeSequence)
                            {
                                writer.WriteLine("r0 = " + branch.R0.ToString("F6"));
                                writer.WriteLine("x0 = " + branch.X0.ToString("F6"));
                                writer.WriteLine("b0 = " + branch.C0.ToString("F6"));
                            }

                            if (scriptType == "py") // py ratings, mileage, and py script line
                            {
                                writer.WriteLine("A = " + Math.Round(currentMVARateA * Math.Sqrt(3) * branch.KV / 1000, 2));
                                writer.WriteLine("B = " + Math.Round(currentMVARateB * Math.Sqrt(3) * branch.KV / 1000, 2));
                                writer.WriteLine("C = " + Math.Round(currentMVARateC * Math.Sqrt(3) * branch.KV / 1000, 2));
                                writer.WriteLine("mile = " + branch.Length);
                                writer.WriteLine();
                                writer.WriteLine("# load data into case:");

                                writer.WriteLine("psspy.branch_data(bus1,bus2,ckt,[status,_i,_i,_i,_i,_i,_i],[r1,x1,b1,A,B,C,_f,_f,_f,_f,_f,_f,mile,_f,_f,_f,_f])");
                            }
                            else // aux Ratings, bus names, and end
                            {
                                writer.Write(Math.Round(currentMVARateA * Math.Sqrt(3) * branch.KV / 1000, 2) + " " + 
                                    Math.Round(currentMvaRateB * Math.Sqrt(3) * branch.KV / 1000, 2) +" ");
                                writer.WriteLine(Math.Round(currentMVARateC * Math.Sqrt(3) * branch.KV / 1000, 2) + " // \" " + 
                                    tbBus1Name.Text + "\" \"" + tbBus2Name.Text + "\"");
                                writer.WriteLine("}");
                            }

                            if (scriptType == "py" & includeSequence)
                            {
                                writer.WriteLine("psspy.seq_branch_data(bus1,bus2,ckt,[r0,x0,b0,_f,_f,_f,_f])");
                            }

                            writer.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(("Exception while building output file: " + ex.Message),
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(("Cannot create output file. No line sections in list."),
                    "Script Writer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        //private void OnPythonFileOptionChange(object sender, EventArgs e)
        //{
        //    if (radPythonAppendFile.Checked)
        //    {
        //        btnPython.Text = "Append to File";
        //    }
        //    else
        //    {
        //        btnPython.Text = "Build File";
        //    }
        //}

        #endregion

        #region Structure Builder

        private void OnStructureEdit(object sender, EventArgs e)
        {
            //get structure name from textbox:
            structName = cmbStructureName.Text.Trim();
            Structure_JWR structure = new Structure_JWR(structName, structFile);
            StructureBuilder builder = new StructureBuilder(structure);
            DialogResult result = builder.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.structure = builder.Structure; // get edited structure object from builder
                isCustomStructure = "True"; // set custom structure flag

                AddCustomStructure(builder.Structure);

                LoadStructureList();

                cmbStructureName.Text = this.structure.Name;

                DrawStructure();
            }
        }

        private void AddCustomStructure(Structure_JWR s)
        {
            try
            {
                branchDataset.CustomStructure.AddCustomStructureRow(
                    s.Name,
                    s.A[0].X, s.A[0].Y,
                    s.A[1].X, s.A[1].Y,
                    s.A[2].X, s.A[2].Y,
                    s.G[0].X, s.G[0].Y,
                    s.G[1].X, s.G[1].Y);
            }
            catch (Exception ex)
            {
                MessageBox.Show(("Unknown Exception: " + ex.Message),
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Context Menus

        private void OnCopyOutput(object sender, EventArgs e)
        {
            Clipboard.SetText(tbImpedances.SelectedText);
        }

        private void OnOutputSelectAll(object sender, EventArgs e)
        {
            tbImpedances.SelectAll();
        }

        #endregion

        private void OnExitApp(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OnKVBaseDropDown(object sender, EventArgs e)
        {
            cmbKVBase.ForeColor = Color.Black;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox aBox = new AboutBox();
            aBox.Show();
        }

        private void dataGridSections_MouseClick(object sender, MouseEventArgs e)
        {
            // handle right click with context menu:
            if (e.Button == MouseButtons.Right)
            {
                // make the hittest row the selected row:
                dataGridSections.ClearSelection();
                int hitrow = dataGridSections.HitTest(e.X, e.Y).RowIndex;
                foreach (DataGridViewRow row in dataGridSections.Rows)
                {
                    if (row.Index == hitrow)
                    {
                        row.Selected = true;
                    }
                }

                // show context menu:
                ctxGridMenu.Show(dataGridSections, new Point(e.X, e.Y));
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnDeleteSelected(sender, e);
        }

        private void OnDeleteSelected(object sender, EventArgs e)
        {
            int cnt = dataGridSections.SelectedRows.Count;

            string msg = "No selected sections to delete.";
            DialogResult result = DialogResult.Cancel;

            if (cnt < 1)
            {
                MessageBox.Show(msg, "None Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }
            else if (cnt == 1)
            {
                msg = "Are you sure you want to delete the selected Line Section from the list?\nClick OK to delete, Cancel to abort.";
                result = MessageBox.Show(msg, "Confirm Delete Selected",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }

            if (result == DialogResult.OK)
            {
                foreach (DataGridViewRow row in dataGridSections.SelectedRows)
                {
                    dataGridSections.Rows.Remove(row);
                }

                string msg2 = "error";

                if (cnt == 0)
                {
                    msg2 = "No selected records to delete.";
                    MessageBox.Show(msg2, "Confirm Delete Selected",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1);
                }

                //else if (cnt == 1)
                //{
                //    msg2 = "1 selected record deleted.";
                //}
                //else if (cnt > 1)
                //{
                //    msg2 = cnt + " selected records deleted.";
                //}

                //MessageBox.Show(msg2, "Confirm Delete Selected",
                //    MessageBoxButtons.OKCancel,
                //    MessageBoxIcon.Warning,
                //    MessageBoxDefaultButton.Button1);

                try
                {
                    isCustomStructure = "False";

                    foreach (DataSet1.CustomStructureRow row in branchDataset.CustomStructure.Rows)
                    {
                        if (row.Name.Equals(structure.Name))
                        {
                            isCustomStructure = "True";
                        }
                    }

                    mileage = Convert.ToDouble(tbMileage.Text);

                    //branchDataset.LineSection.AddLineSectionRow(condName, staticName, structName, mileage, isCustomStructure);
                    dataGridSections.DataSource = branchDataset.LineSection;
                    CalculateImpedances(false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(("Unknown Exception: " + ex.Message),
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void deleteAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to clear all recorded line sections?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                branchDataset.LineSection.Clear();
                dataGridSections.DataSource = branchDataset.LineSection;
            }

            CalculateImpedances(false);
        }

        private void textBoxMOT_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar.ToString() == "\r")
            {
                OnCondOrSeasonChange();
            }
        }

        private void cmbStructureName_Leave(object sender, EventArgs e)
        {
            if (cmbStructureName.Text.Length < 15)
            {
                cmbStructureName.Text = fullStructName;
            }
        }

        private void cmbCondName_SelectedIndexChanged(object sender, EventArgs e)
        {
            // ==================== ACSS UPDATE (2026-02-23) ====================
            // Added ACSS MOT guidance so operators can see the new conductor limit.
            // ===================================================================
            toolStripStatusLabel1.Text =
                "Max MOTs: ACSR and CU=125°C, CU-Hytherm=150°C, ACCC=200°C, ACCR=240°C, ACSS=250°C";
        }

        private void LineSectionBindingSource1_CurrentChanged(object sender, EventArgs e)
        {
        }

        private void radPythonSeq_CheckedChanged(object sender, EventArgs e)
        {
            btnAux.Enabled = false;
        }

        private void radPythonRaw_CheckedChanged(object sender, EventArgs e)
        {
            btnAux.Enabled = true;
        }
    }
}