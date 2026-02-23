using System;
using System.Colletions.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using NPlot;
using TLineCalc;

// namespace LineRatingCalc
// {
//      #region Enums
//      public enum OutputFormat { Normal, Bold, Italic, Warning, Error };
//      public enum TLineVariable { ConductorTemperature, Current, AmbientTemperature, Emissivity, Absorptivity, Diameter, WindVelocity, LineWindAngle, DayOfYear, HourOfDay};
// #endregion

public class LineRatingCalc
{
    #region Data Members

    private double[] alphaCoefs = new double[] { 0.288, 0.1045, -0.0077, 0.0002, -2.0E-06, -7.0E-09 };
    private double[] epsCoefs = new double[] { 0.5332, 0.167, -0.0297, 0.0026, -0.0001, 3E-06, -3E-08 };

    private Assembly assembly;
    private TextReader reader;

    //private string[] sweepable = new string[]
    //{
    //    "Cond Temp", "Current", "Ambient Temp", "Emissivity",
    //    "Absorptivity", "Diameter", "Wind Speed",
    //    "Wind/Line Angle", "Day of Year", "Hour"
    //};

    //private string[] plotable = new string[] { "Cond Temp", "Current" };
    //private int def_sweepIndex = 0;
    //private int def_plotIndex = 1;

    private Dictionary<string, Dictionary<string, double>> condDictionary;
    private string[] condNames;
    private double Imax_old;

    //private bool useMetricUnits;

    private static double foot2meter = 3.280840;
    private static double inch2mm = 25.4;
    private static double mile2meter = 1609.344;

    // Specific heats:
    // *** Material     *** Cp [W-sec/kg-degC]   *** Cp [W-sec/lb-degC] ***
    // *** Al           *** 954                  *** 433
    // *** Cu           *** 424                  *** 192
    // *** Steel        *** 477                  *** 216
    // *** CarbonFiber  *** 813                  *** 368.77
    // *** Alumoweld    *** 532                  *** 242

    private static int CpAl = 433;
    private static int CpCu = 192;
    private static int CpSteel = 216;               
    // private static int CpAlumoweld = 242;
    private static int CpCarbonFiber = 369;         // This number was received from Eric Bosze at CTC Global.

    private double diameter = 1, R1 = 1, R2 = 2, heatCapacity, T1, T2;
    public double rateA, rateB, rateC;

    private TLineThermalModel model;

    //private bool armed = false;
    //private bool verbose = true;
    //private bool autoVerbose = false;

    //private bool Armed
    //{
    //    get
    //    {
    //        return armed;
    //    }
    //    set
    //    {
    //        if (value)
    //        {
    //            armed = true;
    //        }
    //        else
    //        {
    //            armed = false;
    //        }
    //    }
    //}

    //public bool Verbose
    //{
    //    get
    //    {
    //        return verbose;
    //    }
    //    set
    //    {
    //        verbose = value;
    //    }
    //}

    //public bool AutoVerbose
    //{
    //    get
    //    {
    //        return autoVerbose;
    //    }
    //    set
    //    {
    //        autoVerbose = value;
    //    }
    //}

    #endregion

    #region Constructors

    #endregion

    #region Initialization Methods

    public void InitializeCalculator()
    {
        //Armed = false;

        //useMetricUnits = true;
        ResetModel();
        SolveSteadyStateCurrent(true, 40, 125, "");
        //SolveSweep();
        LoadConductors("");
        //SolveTransientCurrent();
        //ResetMessageBox();

        //Armed = true;

        //setSCEGDefaults();
    }

    //public void setSCEGDefaults()
    //{
    //    // Change defaults per SCEG's FRM
    //    //toolMetricUnits.Checked = false;
    //    //HeightSeaLevel.Value = 500;
    //    //Latitude.Value = 32.23M;
    //
    //    //AmbientTemperatureDegF.Value = AmbientTemperature.Value * 9 / 5 + 32;
    //    //MaxTemperature.Value = 100;
    //}

    private void LoadConductors(string condDataFileName)
    {
        condDictionary = new Dictionary<string, Dictionary<string, double>>();

        try
        {
            assembly = Assembly.GetExecutingAssembly();
            //reader = new StreamReader(assembly.GetManifestResourceStream("TLICs.Resources.ConductorData.txt"));
            reader = new StreamReader(Application.StartupPath + @"\Resources\ConductorData.txt");

            if (condDataFileName != "")
            {
                reader = new StreamReader(condDataFileName);
            }

            string[] fields = null;

            string line = reader.ReadLine(); // header
            fields = line.Split(new char[] { ',', }, StringSplitOptions.RemoveEmptyEntries);

            int codeLoc = 0, typeLoc = 0, sizeLoc = 0, DLoc = 0,
                LBS_kft_OuterLoc = 0, LBS_kft_InnerLoc = 0,
                R25Loc = 0, R75Loc = 0;

            for (int i = 0; i < fields.Length; i++)
            {
                switch (fields[i].ToUpper())
                {
                    case "CODE_NAME":
                        codeLoc = i;
                        break;
                    case "TYPE":
                        typeLoc = i;
                        break;
                    case "SIZE":
                        sizeLoc = i;
                        break;
                    case "OD_IN":
                        DLoc = i;
                        break;
                    case "LBS_KFT_OUTER":
                        LBS_kft_OuterLoc = i;
                        break;
                    case "LBS_KFT_INNER":
                        LBS_kft_InnerLoc = i;
                        break;
                    case "R25":
                        R25Loc = i;
                        break;
                    case "R75":
                        R75Loc = i;
                        break;
                    default:
                        break;
                }
            }

            double D, LBS_kft_Outer, LBS_kft_Inner, mCp, R25, R75 = 0.0;
            //double[] data
            string code, type, size, name = "";

            line = reader.ReadLine(); // first row

            Dictionary<string, double> rowData = null;

            while (line.Trim().Length > 0) // while row not all whitespace:
            {
                fields = line.Split(new char[] { ',' });

                code = fields[codeLoc];
                type = fields[typeLoc];
                size = fields[sizeLoc];

                if (!size.Equals("0"))
                {
                    name = size + " " + type + " (" + code + ")";
                }
                else
                {
                    name = code + " (" + type + ")";
                }

                R25 = Convert.ToDouble(fields[R25Loc]);
                R75 = Convert.ToDouble(fields[R75Loc]);
                D = Convert.ToDouble(fields[DLoc]);
                LBS_kft_Outer = Convert.ToDouble(fields[LBS_kft_OuterLoc]);
                LBS_kft_Inner = Convert.ToDouble(fields[LBS_kft_InnerLoc]);

                if (R75 > 999.9)
                {
                    R75 = R25 * 1.202;
                }

                // Calc heat capacity (mCp [W-sec/ft-degC] or [W-sec/m-degC])
                if (type.ToUpper().Equals("CU"))
                {
                    mCp = LBS_kft_Outer / 1000 * CpCu;
                }
                else if (type.ToUpper().Equals("ACCC"))
                {
                    mCp = LBS_kft_Outer / 1000 * CpAl
                          + LBS_kft_Inner / 1000 * CpCarbonFiber;
                }
                else // All else has Al conductor. LBS_kft_Inner will be 0 if unimetal.
                {
                    mCp = LBS_kft_Outer / 1000 * CpAl
                          + LBS_kft_Inner / 1000 * CpSteel;
                }

                D = D * inch2mm;
                R25 = R25 / mile2meter;
                R75 = R75 / mile2meter;
                mCp = mCp * foot2meter;

                rowData = new Dictionary<string, double>();
                rowData.Add("R25", R25);
                rowData.Add("R75", R75);
                rowData.Add("D", D);
                rowData.Add("HeatCap", mCp);

                condDictionary.Add(name.ToLower(), rowData);

                line = reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                // udMVA0.Value = Convert.ToDecimal(comboboxTransVolt.Text) * Convert.ToDecimal(Math.Sqrt(3)) * udI0.Value / Convert.ToDecimal(1000);
            }

            reader.Close();
            reader = null;

            condNames = new string[condDictionary.Keys.Count];
            condDictionary.Keys.CopyTo(condNames, 0);

            //cbCond.DataSource = condNames;
            //cbCond.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            reader.Close();
            reader = null;
            MessageBox.Show("Error accessing lookup data: " + ex.Message, "Data Load Error");
        }
    }

    private void ResetModel()
    {
        // Reset default sweep variables:
        //cmbSweep.DataSource = sweepable;
        //cmbPlot.DataSource = plotable;

        //cmbSweep.SelectedIndex = def_sweepIndex;
        //cmbPlot.SelectedIndex = def_plotIndex;

        //tbLowValue.Text = def_lowValue.ToString("F1");
        //tbHighValue.Text = def_highValue.ToString("F1");

        model = new TLineThermalModel();

        try
        {
            T1 = model.TLow;
            T2 = model.THigh;
        }
        catch (FormatException ex)
        {
            MessageBox.Show(
                "An input field's value is in the incorrect format. " + ex.Message,
                "Format Exception",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "An unknown exception has occured. " + ex.Message,
                "Unknown Exception",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        SolveSteadyStateCurrent(true, 40, 125, "");

        //Imax_new = (double)Imax.Value;
        //Imax_old = (double)Imax.Value;
    }

    //private void ResetMessageBox()
    //{
    //    tbOutput.Clear();
    //
    //    tbOutput.ReadOnly = true;
    //    tbOutput.ScrollBars = RichTextBoxScrollBars.Both;
    //
    //    tbOutput.Font = new Font("Courier New", 10.0F);
    //
    //    tbOutput.ForeColor = Color.Black;
    //    tbOutput.BackColor = Color.White;
    //    tbOutput.Font = new Font(tbOutput.Font, FontStyle.Bold);
    //}

    #endregion

    #region Input Change Delegates

    public string selectConductorSolve(bool isSummer, string condName, double ambTemp, double maxTemperature)
    {
        string cond = condName, bundledOrCondType = "";

        try
        {
            if (cond.Substring(0, 1).ToLower().Equals("b")) // bundled conductor has twice the rating
            {
                cond = cond.Substring(1, cond.Length - 1);
                bundledOrCondType = "b";
            }
            else if (cond.Substring(0, 2).ToLower().Equals("tb")) // triple bundled conductor has thrice the rating
            {
                cond = cond.Substring(2, cond.Length - 2);
                bundledOrCondType = "tb";
            }
            else if (cond.Substring(0, 2).ToLower().Equals("hy")) // hytherm copper conductor has higher HOTs (25 degC higher for A, B and C)
            {
                cond = cond.Substring(2, cond.Length - 2);
                bundledOrCondType = "hy";
            }
            else if (cond.Contains("accc")) // accc conductor has higher MOTs (180, 200, 200)
            {
                bundledOrCondType = "accc";
            }
            else if (cond.Contains("accr")) // accr conductor has higher MOTs (210, 240, 240)
            {
                bundledOrCondType = "accr";
            }
            // ==================== ACSS UPDATE (2026-02-23) ====================
            // New conductor type support requested:
            // ACSS has MOT caps for ratings A/B/C of 200/250/250 degC.
            // ===================================================================
            else if (cond.Contains("acss")) // acss conductor has higher MOTs (200, 250, 250)
            {
                bundledOrCondType = "acss";
            }
        }
        catch
        {
        }

        try
        {
            diameter = condDictionary[cond]["D"];
            R1 = condDictionary[cond]["R25"];
            R2 = condDictionary[cond]["R75"];
            heatCapacity = condDictionary[cond]["HeatCap"];

            SolveSteadyStateCurrent(isSummer, ambTemp, maxTemperature, bundledOrCondType);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return "";
    }

    //private void OnTabledCondPropertiesChanged(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        SolveEverythingCurrent();
    //        Output("Conductor Properties Changed.");
    //        Output("Max Current is Now: " + Imax.Value.ToString("F1"));
    //        cbCond.Text = "Custom";
    //    }
    //}

    //private void OnVariableCondPropertiesChanged(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        SolveEverythingCurrent();
    //        Output("Conductor Properties Changed.");
    //        Output("Max Current is Now: " + Imax.Value.ToString("F1"));
    //    }
    //}

    //private void OnConstructionDataChanged(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        SolveEverythingCurrent();
    //        Output("Line Construction Data Changed.");
    //        Output("Max Current is Now: " + Imax.Value.ToString("F1"));
    //    }
    //}

    //private void OnWeatherDataChanged(object sender, EventArgs e)
    //{
    //    bool verbose0 = Verbose;
    //
    //    if (Armed)
    //    {
    //        Verbose = true;
    //        weatherDataChanged();
    //    }
    //
    //    Verbose = verbose0;
    //}

    //private void weatherDataChanged()
    //{
    //    SolveEverythingCurrent();
    //    if (Verbose && !AutoVerbose)
    //    {
    //        Output("Weather Information Changed.");
    //        Output("Max Current is Now: " + Imax.Value.ToString("F1"));
    //    }
    //}

    //private void OnImaxChanged(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        Armed = false;
    //
    //        SolveSteadyStateTemperature();
    //
    //        Armed = true;
    //    }
    //}

    //private void OnCalcTransient(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        Armed = false;
    //
    //        SolveTransientCurrent();
    //
    //        Armed = true;
    //    }
    //}

    //private void OnMaxTransCurrentChanged(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        Output("User adjusting Transient Current Limit not yet Supported.", OutputFormat.Warning);
    //
    //        //if (solveTransTime)
    //        //{
    //        //    SolveTransientTime();
    //        //}
    //        //else if (solveTransPreload)
    //        //{
    //        //    SolveTransientPreloading();
    //        //}
    //        //else
    //        //{
    //        //    SolveTransient
    //        //}
    //    }
    //}

    //private void OnSweep(object sender, EventArgs e)
    //{
    //    if (Armed)
    //    {
    //        Output("Plotting Variable Sensitivity . . .", OutputFormat.Bold);
    //
    //        Armed = false;
    //
    //        SolveSweep();
    //
    //        Armed = true;
    //    }
    //}

    //private void OnBrowseCondFile(object sender, EventArgs e)
    //{
    //    DialogResult result = openDialog.ShowDialog();
    //
    //    if (result == DialogResult.OK)
    //    {
    //        LoadConductors(openDialog.FileName);
    //    }
    //}

    //private void OnAgeChanged(object sender, EventArgs e)
    //{
    //    if (Armed && useAge)
    //    {
    //        Armed = false;
    //
    //        double age;
    //        age = Convert.ToDouble(udAge.Text);
    //
    //        double epsA = 0.91;
    //        double epsB = 5.50;
    //        double alphaA = 0.54;
    //        double alphaB = 7.40;
    //
    //        if (age <= 0.0) age = 0.001;
    //
    //        decimal eps = (decimal)((epsA * Math.Log(age) + epsB) * 0.1);
    //        decimal alpha = (decimal)((alphaA * Math.Log(age) + alphaB) * 0.1);
    //
    //        if (eps > 0.91M) eps = 0.91M;
    //        if (alpha > 0.91M) alpha = 0.91M;
    //
    //        if (eps < 0.30M) eps = 0.30M;
    //        if (alpha < 0.10M) alpha = 0.10M;
    //
    //        Emissivity.Value = eps;
    //        Absorptivity.Value = alpha;
    //
    //        Armed = true;
    //
    //        SolveEverythingCurrent();
    //    }
    //}

    //private void OnUseAgeChange(object sender, EventArgs e)
    //{
    //    if (useAge)
    //    {
    //        if (!chkUseAge.Checked)
    //        {
    //            useAge = false;
    //            udAge.Enabled = false;
    //
    //            bool armed0 = Armed;
    //            Armed = true;
    //
    //            Emissivity.Value = (decimal)oldEps;
    //            Absorptivity.Value = (decimal)oldAlpha;
    //
    //            Armed = armed0;
    //        }
    //    }
    //    else
    //    {
    //        if (chkUseAge.Checked)
    //        {
    //            useAge = true;
    //            udAge.Enabled = true;
    //
    //            oldEps = (double)Emissivity.Value;
    //            oldAlpha = (double)Absorptivity.Value;
    //
    //            bool armed0 = Armed;
    //            Armed = true;
    //
    //            OnAgeChanged(sender, e);
    //
    //            Armed = armed0;
    //        }
    //    }
    //}

    #endregion

    #region Output Box Methods

    //private void Output(string text)
    //{
    //    if (Verbose)
    //    {
    //        OutputTimeStamp();
    //
    //        tbOutput.AppendText(text + "\n");
    //
    //        tbOutput.ScrollToCaret();
    //    }
    //}

    //private void Output(string text, OutputFormat format)
    //{
    //    if (Verbose)
    //    {
    //        OutputTimeStamp();
    //
    //        if (format == OutputFormat.Bold)
    //        {
    //            tbOutput.SelectionColor = Color.Black;
    //            tbOutput.SelectionBackColor = Color.White;
    //            tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Bold);
    //        }
    //        else if (format == OutputFormat.Italic)
    //        {
    //            tbOutput.SelectionColor = Color.Black;
    //            tbOutput.SelectionBackColor = Color.White;
    //            tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Italic);
    //        }
    //        else if (format == OutputFormat.Warning)
    //        {
    //            tbOutput.SelectionColor = Color.OrangeRed;
    //            tbOutput.SelectionBackColor = Color.LightYellow;
    //            tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Regular);
    //        }
    //        else if (format == OutputFormat.Error)
    //        {
    //            tbOutput.SelectionColor = Color.Red;
    //            tbOutput.SelectionBackColor = Color.Pink;
    //            tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Bold);
    //        }
    //
    //        tbOutput.AppendText(text + "\n");
    //        tbOutput.ScrollToCaret();
    //    }
    //}

    //private void OutputTimeStamp()
    //{
    //    tbOutput.SelectionColor = Color.Blue;
    //    tbOutput.SelectionBackColor = Color.SeaShell;
    //    tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Regular);
    //
    //    DateTime time = DateTime.Now;
    //    tbOutput.AppendText(time.ToString("hh:mm:ss"));
    //
    //    tbOutput.SelectionColor = Color.Black;
    //    tbOutput.SelectionBackColor = Color.White;
    //    tbOutput.SelectionFont = new Font(tbOutput.Font, FontStyle.Regular);
    //
    //    tbOutput.AppendText("  ");
    //}

    //private void PrintChange()
    //{
    //    double change = 100.0 * (Imax_old - Imax_new) / Imax_old;
    //    if (!double.IsInfinity(change))
    //    {
    //        Output("Change: " + change.ToString("F2") + "%");
    //    }
    //}

    #endregion

    #region Solution Methods

    private void SolveSteadyStateCurrent(bool isSummer, double ambTemp, double maxTemperature, string bundledOrCondType)
    {
        double tempA = 94, tempB = 100;
        try
        {
            model.Diameter = diameter;
            model.Absorptivity = 0.91;
            model.Emissivity = 0.91;
            model.RLow = R1;
            model.RHigh = R2;
            model.TLow = T1;
            model.THigh = T2;

            if (isSummer)
            {
                model.Month = 7; // July
            }
            else
            {
                model.Month = 3; // March
            }

            model.AmbientTemperature = ambTemp;
            model.DayOfMonth = 1;
            model.Hour = 12;
            model.Height = 152;
            model.Latitude = 32.234;
            model.LineAzimuth = 0;
            model.LineWindAngle = 45;

            if (bundledOrCondType == "hy")
            {
                maxTemperature = maxTemperature + 25;
                tempA = 94 + 25;
                tempB = 100 + 25;
            }
            else if (bundledOrCondType == "accc")
            {
                tempA = maxTemperature;
                tempB = maxTemperature;

                if (maxTemperature > 180)
                {
                    tempA = 180;
                }

                if (maxTemperature > 200)
                {
                    tempB = 200;
                }
            }
            else if (bundledOrCondType == "accr")
            {
                tempA = maxTemperature;
                tempB = maxTemperature;

                if (maxTemperature > 210)
                {
                    tempA = 210;
                }

                if (maxTemperature > 240)
                {
                    tempB = 240;
                }
            }
            // ==================== ACSS UPDATE (2026-02-23) ====================
            // ACSS Rating Temperature Rules:
            // - Rating A cap: 200 degC
            // - Rating B cap: 250 degC
            // - Rating C cap: 250 degC (enforced by capping maxTemperature)
            // ===================================================================
            else if (bundledOrCondType == "acss")
            {
                tempA = maxTemperature;
                tempB = maxTemperature;

                if (maxTemperature > 200)
                {
                    tempA = 200;
                }

                if (maxTemperature > 250)
                {
                    tempB = 250;
                    maxTemperature = 250;
                }
            }

            model.MaxTemperature = maxTemperature;
            model.WindVelocity = 0.61;
            model.IsClearAtmosphere = true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show("An input field's value is in the incorrect format. " + ex.Message,
                            "Format Exception",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show("An unknown exception has occured. " + ex.Message,
                            "Unknown Exception",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
        }

        try
        {
            model.SolveSteadyState();

            rateC = 0;
            if (model.Imax != double.NaN)
            {
                if (bundledOrCondType == "b")
                {
                    rateC = model.Imax * 2; // bundled conductor has twice the rating
                }
                else if (bundledOrCondType == "tb")
                {
                    rateC = model.Imax * 3; // triple bundled conductor has thrice the rating
                }
                else
                {
                    rateC = model.Imax;
                }
            }

            rateB = rateC;
            if (maxTemperature > tempB)
            {
                model.MaxTemperature = tempB;

                model.SolveSteadyState();

                if (model.Imax != double.NaN)
                {
                    if (bundledOrCondType == "b")
                    {
                        rateB = model.Imax * 2; // bundled conductor has twice the rating
                    }
                    else if (bundledOrCondType == "tb")
                    {
                        rateB = model.Imax * 3; // triple bundled conductor has thrice the rating
                    }
                    else
                    {
                        rateB = model.Imax;
                    }
                }
            }

            rateA = rateB;
            if (maxTemperature > tempA)
            {
                model.MaxTemperature = tempA;

                model.SolveSteadyState();

                if (model.Imax != double.NaN)
                {
                    if (bundledOrCondType == "b")
                    {
                        rateA = model.Imax * 2; // bundled conductor has twice the rating
                    }
                    else if (bundledOrCondType == "tb")
                    {
                        rateA = model.Imax * 3; // triple bundled conductor has thrice the rating
                    }
                    else
                    {
                        rateA = model.Imax;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("An exception has occured while solving for steady-state current. " + ex.Message,
                            "Calculation Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
        }
    }

    //private void SolveSteadyStateTemperature()
    //{
    //    double Inew = Convert.ToDouble(Imax.Value);
    //
    //    SolveSteadyStateCurrent();
    //
    //    MaxTemperature.Value = (decimal)model.TemperatureAt(Inew, 0.01);
    //
    //    //here goes the BIG test!!:
    //    Output("User changed Maximum Current:", OutputFormat.Bold);
    //    Output("\tMax Current changed to: " + Imax.Value.ToString("F1"));
    //    Output("\tThe Max Temperature is now: " + MaxTemperature.Value.ToString("F1"));
    //
    //    double I1 = Inew;
    //
    //    Output("\tRecalculating Current from new Temperature . . .");
    //
    //    SolveSteadyStateCurrent();
    //
    //    double I2 = (double)Imax.Value;
    //
    //    if (Math.Abs(I1 - I2) > 0.1)
    //    {
    //        Output("\tUnsuccessful calculation, Solution did not converge!", OutputFormat.Error);
    //        Output("\tCurrent from T=f(I) calculation = " + I1.ToString("F1"), OutputFormat.Error);
    //        Output("\tCurrent from I=f(T) calculation = " + I2.ToString("F1"), OutputFormat.Error);
    //        Output("\tDifference is greater than tolerance! (0.1 degC)", OutputFormat.Error);
    //    }
    //    else
    //    {
    //        Output("\tSuccessful calculation, Solution converged", OutputFormat.Bold);
    //        Output("\tCurrent from T=f(I) calculation = " + I1.ToString("F1"), OutputFormat.Italic);
    //        Output("\tCurrent from I=f(T) calculation = " + I2.ToString("F1"), OutputFormat.Italic);
    //        Output("\tDifference is within tolerance (0.1 degC)", OutputFormat.Italic);
    //    }
    //}

    //private void SolveSweep()
    //{
    //    try
    //    {
    //        int sweepIndex = cmbSweep.SelectedIndex;
    //        int plotIndex = cmbPlot.SelectedIndex;
    //        double low = Convert.ToDouble(tbLowValue.Text);
    //        double high = Convert.ToDouble(tbHighValue.Text);
    //
    //        Output("\tSweeping [" + cmbSweep.Text + "] from " + low.ToString("F2")
    //            + " to " + high.ToString("F2"));
    //        Output("\tPlotting: [" + cmbPlot.Text + "]");
    //
    //        TLineThermalModel model2 = (TLineThermalModel)model.Clone();
    //
    //        TLineVariableSweeper sweeper = new TLineVariableSweeper(model2, (TLineVariable)sweepIndex,
    //                                                                (TLineVariable)plotIndex, low, high);
    //
    //        double[] xData = sweeper.Xdata;
    //        double[] yData = sweeper.Ydata;
    //
    //        int maxIndex = 0;
    //        double lastY = double.NegativeInfinity;
    //        for (int i = 0; i < yData.Length; i++)
    //        {
    //            if (yData[i] > lastY && !double.IsNaN(yData[i]))
    //            {
    //                lastY = yData[i];
    //                maxIndex = i;
    //            }
    //        }
    //
    //        int minIndex = 0;
    //        lastY = double.PositiveInfinity;
    //        for (int i = 0; i < yData.Length; i++)
    //        {
    //            if (yData[i] < lastY && !double.IsNaN(yData[i]))
    //            {
    //                lastY = yData[i];
    //                minIndex = i;
    //            }
    //        }
    //
    //        //print max value:
    //        Output("\tMaximum [" + cmbPlot.Text + "] is: " + yData[maxIndex].ToString("F2"), OutputFormat.Italic);
    //        Output("\tMaximum [" + cmbPlot.Text + "] occurs at [" + cmbSweep.Text + "] = " +
    //               xData[maxIndex].ToString("F2"), OutputFormat.Italic);
    //
    //        //print max value:
    //        Output("\tMinimum [" + cmbPlot.Text + "] is: " + yData[minIndex].ToString("F2"), OutputFormat.Italic);
    //        Output("\tMinimum [" + cmbPlot.Text + "] occurs at [" + cmbSweep.Text + "] = " +
    //               xData[minIndex].ToString("F2"), OutputFormat.Italic);
    //
    //        //Clear plot surface:
    //        plotSurface2.Clear();
    //
    //        //Create line plots:
    //        NPlot.LinePlot plot = new LinePlot(yData, xData);
    //
    //        //Add plots to plot surface:
    //        plot.Pen = new Pen(Color.DarkBlue, 3.0F);
    //
    //        //Gridlines:
    //        NPlot.Grid grid = new Grid();
    //        grid.VerticalGridType = Grid.GridType.Fine;
    //        grid.HorizontalGridType = Grid.GridType.Fine;
    //
    //        NPlot.Legend legend = new Legend();
    //
    //        plotSurface2.Add(grid);
    //        plotSurface2.Add(plot);
    //
    //        //Set Bounds;
    //        plotSurface2.XAxis1.WorldMin = plot.SuggestXAxis().WorldMin;
    //        plotSurface2.XAxis1.WorldMax = plot.SuggestXAxis().WorldMax;
    //        plotSurface2.YAxis1.WorldMin = plot.SuggestYAxis().WorldMin;
    //        plotSurface2.YAxis1.WorldMax = plot.SuggestYAxis().WorldMax;
    //
    //        plotSurface2.XAxis1.Label = cmbSweep.Text;
    //        plotSurface2.YAxis1.Label = cmbPlot.Text;
    //
    //        //Force Redraw:
    //        plotSurface2.PlotBackColor = Color.Snow;
    //        plotSurface2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
    //        plotSurface2.Refresh();
    //    }
    //    catch (Exception ex)
    //    {
    //        Output("\tError: " + ex.Message, OutputFormat.Error);
    //    }
    //}

    //private void SolveTransientCurrent()
    //{
    //    Output("Attempting Transient Solution . . .", OutputFormat.Bold);
    //
    //    try
    //    {
    //        double maxTime = Convert.ToDouble(udMaxTime.Value);
    //        double i0 = Convert.ToDouble(udI0.Value);
    //        double tempMax = Convert.ToDouble(MaxTemperature.Value);
    //        double mCp = Convert.ToDouble(HeatCapacity.Value);
    //
    //        model.MaxTemperature = tempMax;
    //        model.TransientInitialCurrent = i0;
    //        model.TransientMaxTime = maxTime * 60.0;
    //        model.TransientHeatCapacity = mCp;
    //
    //        if (!toolMetricUnits.Checked)
    //        {
    //            model.TransientHeatCapacity = (double)HeatCapacity.Value * foot2meter;
    //        }
    //
    //        //first solve for tau:
    //        model.SolveTransientTauMethod();
    //
    //        double[] x_data;
    //        double[] y_data;
    //
    //        // get tau method solution results:
    //        double tau = model.TransientTimeConstant;
    //        double ti = model.TransientInitialTemp;
    //        double tf = model.TransientFinalTemp;
    //
    //        if (radSim.Checked) // sim method:
    //        {
    //            // set time step for a reasonable resolution (min of tau/100 and tmax/100):
    //            //double dt1 = model.TransientMaxTime / 100.0;
    //            //double dt2 = model.TransientTimeConstant / 100.0;
    //            //model.TransientTimeStep = Math.Min(dt1, dt2);
    //
    //            // Solve via simultation method and get data vectors:
    //            model.SolveTransientSimulationMethod();
    //            x_data = model.TransientTimeData;
    //            y_data = model.TransientTemperatureData;
    //
    //            // convert time points to minutes  for graph:
    //            for (int i = 0; i < x_data.Length; i++)
    //            {
    //                x_data[i] = x_data[i] / 60.0;
    //            }
    //        }
    //        else
    //        {
    //            int points = 120;
    //            double t0 = -30.0;
    //            double dt = 1.0;
    //
    //            x_data = new double[points];
    //            y_data = new double[points];
    //
    //            double t = t0;
    //
    //            for (int i = 0; i < points; i++)
    //            {
    //                if (t == 0.0) {
    //                    x_data[i] = 0.0;
    //                } else {
    //                    x_data[i] = t;
    //                }
    //
    //                if (t < 0.0) {
    //                    y_data[i] = ti;
    //                } else {
    //                    y_data[i] = ti + (tf - ti) * (1.0 - Math.Exp(-t * 60.0 / tau));
    //                }
    //                t += dt;
    //            }
    //        }
    //
    //        plotSurface.Clear();
    //
    //        NPlot.LinePlot plot = new LinePlot(y_data, x_data);
    //
    //        NPlot.Grid grid = new Grid();
    //        grid.VerticalGridType = Grid.GridType.Fine;
    //        grid.HorizontalGridType = Grid.GridType.Fine;
    //
    //        plotSurface.Add(grid);
    //
    //        plot.Pen = new Pen(Color.DarkBlue, 3.0F);
    //        plot.Label = "Cond Temp";
    //
    //        plotSurface.Add(plot);
    //
    //        plotSurface.XAxis1.WorldMin = plot.SuggestXAxis().WorldMin;
    //        plotSurface.XAxis1.WorldMax = plot.SuggestXAxis().WorldMax;
    //        plotSurface.YAxis1.WorldMin = plot.SuggestYAxis().WorldMin;
    //        plotSurface.YAxis1.WorldMax = plot.SuggestYAxis().WorldMax;
    //
    //        NPlot.LinePlot maxTimePlot = new LinePlot(
    //            new double[] { plotSurface.YAxis1.WorldMin, plotSurface.YAxis1.WorldMax },
    //            new double[] { maxTime, maxTime });
    //
    //        NPlot.LinePlot minTempPlot = new LinePlot(
    //            new double[] { model.TransientInitialTemp, model.TransientInitialTemp },
    //            new double[] { plotSurface.XAxis1.WorldMin, plotSurface.XAxis1.WorldMax });
    //
    //        NPlot.LinePlot timeTempPlot = new LinePlot(
    //            new double[] { model.MaxTemperature, model.MaxTemperature },
    //            new double[] { plotSurface.XAxis1.WorldMin, plotSurface.XAxis1.WorldMax });
    //
    //        NPlot.LinePlot maxTempPlot = new LinePlot(
    //            new double[] { model.TransientFinalTemp, model.TransientFinalTemp },
    //            new double[] { plotSurface.XAxis1.WorldMin, plotSurface.XAxis1.WorldMax });
    //
    //        maxTimePlot.Pen = new Pen(Color.DarkRed, 2.0F);
    //        timeTempPlot.Pen = new Pen(Color.DarkGreen, 2.0F);
    //
    //        maxTimePlot.Label = "Max Time";
    //        timeTempPlot.Label = "Max Temp";
    //
    //        plotSurface.Add(maxTimePlot);
    //        plotSurface.Add(timeTempPlot);
    //
    //        Legend legend = new Legend();
    //        legend.AttachTo(plotSurface.XAxisPosition.Bottom, plotSurface.YAxisPosition.Right);
    //        legend.XOffset = -10;
    //        legend.YOffset = -10;
    //        legend.VerticalEdgePlacement = Legend.Placement.Inside;
    //
    //        plotSurface.Legend = legend;
    //        plotSurface.LegendZOrder = 10;
    //
    //        plotSurface.XAxis1.Label = "time (min)";
    //        plotSurface.YAxis1.Label = "Temp (degC)";
    //
    //        plotSurface.PlotBackColor = Color.Snow;
    //        plotSurface.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
    //        plotSurface.Refresh();
    //
    //        Output("Transient Solution Converged", OutputFormat.Bold);
    //        Output("\tInitial Current = " + model.TransientInitialCurrent.ToString("F1"), OutputFormat.Italic);
    //        Output("\tFinal Current = " + model.TransientFinalCurrent.ToString("F1"), OutputFormat.Italic);
    //        Output("\tInitial Temperature = " + model.TransientInitialTemp.ToString("F1"), OutputFormat.Italic);
    //        Output("\tFinal Temperature = " + model.TransientFinalTemp.ToString("F1"), OutputFormat.Italic);
    //
    //        udMaxTransCurrent.Value = (decimal)model.TransientFinalCurrent;
    //        textBoxTransMVA.Text = (Convert.ToDouble(comboBoxTransVolt.Text) *
    //                                 Math.Sqrt(3) *
    //                                 Convert.ToDouble(udMaxTransCurrent.Value) / 1000).ToString();
    //    }
    //    catch (Exception ex)
    //    {
    //        Output("\tTransient solution did not converge!", OutputFormat.Error);
    //        Output("\tError: " + ex.Message, OutputFormat.Error);
    //        Output("\t(Try reducing current step size or iteration limit)", OutputFormat.Italic);
    //    }
    //}

    //private void SolveEverythingCurrent()
    //{
    //    Cursor = Cursors.WaitCursor;
    //
    //    bool armed0 = Armed;
    //    bool verbose0 = Verbose;
    //
    //    Armed = false;
    //    Verbose = false;
    //
    //    SolveSteadyStateCurrent();
    //    SolveSweep();
    //
    //    if (udI0.Value >= Imax.Value)
    //    {
    //        udI0.Value = 0.8M * Imax.Value;
    //        udMVA0.Value = Convert.ToDecimal(
    //            Convert.ToDouble(udI0.Value) *
    //            Math.Sqrt(3) *
    //            Convert.ToDouble(comboBoxTransVolt.Text) / 1000);
    //        Output("Initial current cannot be greater than steady state current. Initial current set to 80% of previous value.");
    //    }
    //
    //    SolveTransientCurrent();
    //    PrintChange();
    //
    //    Armed = armed0;
    //    Verbose = verbose0;
    //
    //    Cursor = Cursors.Default;
    //}

    #endregion

    #region Application Utilities

    //private void OnUnitsChanged(object sender, EventArgs e)
    //{
    //    Output("Changing Units . . .", OutputFormat.Bold);
    //    Output("\tMax current before unit change: " + Imax.Value.ToString("F2"));
    //
    //    bool armed0 = Armed;
    //
    //    Armed = false;
    //
    //    if (!useMetricUnits) // if previously english:
    //    {
    //        if (toolMetricUnits.Checked) // and now needs to be metric:
    //        {
    //            Output("\tUnits changed from English to Metric");
    //
    //            HeightSeaLevel.Value = HeightSeaLevel.Value / (decimal)foot2meter;
    //            Diameter.Value = Diameter.Value / (decimal)mm2in;
    //            HeatCapacity.Value = HeatCapacity.Value * (decimal)(foot2meter);
    //            R1.Value = R1.Value / (decimal)mile2meter;
    //            R2.Value = R2.Value / (decimal)mile2meter;
    //            WindVelocity.Value = WindVelocity.Value / (decimal)foot2meter;
    //
    //            lblUnitsAltitude.Text = "m";
    //            lblUnitsDiameter.Text = "mm";
    //            lblUnitsHeatCapacity.Text = "W-sec/m-degC";
    //            lblUnitsR1.Text = "ohm/m";
    //            lblUnitsR2.Text = "ohm/m";
    //            lblUnitsWindspeed.Text = "m/s";
    //
    //            //change up-down increment to be appropriate for ohm/m:
    //            R1.Increment = 0.00001M;
    //            R2.Increment = 0.00001M;
    //        }
    //    }
    //    else // if previously metric:
    //    {
    //        if (!toolMetricUnits.Checked) // and now needs to be english:
    //        {
    //            Output("\tUnits changed from Metric to English");
    //
    //            HeightSeaLevel.Value = HeightSeaLevel.Value * (decimal)foot2meter;
    //            Diameter.Value = Diameter.Value * (decimal)mm2in;
    //            HeatCapacity.Value = HeatCapacity.Value / (decimal)(foot2meter);
    //            R1.Value = R1.Value * (decimal)mile2meter;
    //            R2.Value = R2.Value * (decimal)mile2meter;
    //            WindVelocity.Value = WindVelocity.Value * (decimal)foot2meter;
    //
    //            lblUnitsAltitude.Text = "ft";
    //            lblUnitsDiameter.Text = "in";
    //            lblUnitsHeatCapacity.Text = "W-sec/ft-degC";
    //            lblUnitsR1.Text = "ohm/mi";
    //            lblUnitsR2.Text = "ohm/mi";
    //            lblUnitsWindspeed.Text = "ft/s";
    //
    //            //change up-down increment to be appropriate for ohm/mi:
    //            R1.Increment = 0.01M;
    //            R2.Increment = 0.01M;
    //        }
    //    }
    //
    //    //set metric flag:
    //    useMetricUnits = toolMetricUnits.Checked;
    //
    //    Armed = armed0;
    //
    //    SolveEverythingCurrent();
    //
    //    Output("\tMax current after unit change: " + Imax.Value.ToString("F2"));
    //}

    #endregion

    //private void comboBoxSSVolt_SelectedIndexChanged(object sender, EventArgs e)
    //{
    //    textBoxSSMVA.Text = (Convert.ToDouble(comboBoxSSVolt.Text) * Math.Sqrt(3) * Convert.ToDouble(Imax.Value) / 1000).ToString();
    //}

    //private void comboBoxTransVolt_SelectedIndexChanged(object sender, EventArgs e)
    //{
    //    textBoxTransMVA.Text = (Convert.ToDouble(comboBoxTransVolt.Text) * Math.Sqrt(3) * Convert.ToDouble(udMaxTransCurrent.Value) / 1000).ToString();
    //}

    //private void udIO_ValueChanged(object sender, EventArgs e)
    //{
    //    if (udIO.ReadOnly != true)
    //    {
    //        udMVA0.Value = Convert.ToDecimal(Convert.ToDouble(udIO.Value) * Math.Sqrt(3) * Convert.ToDouble(comboBoxTransVolt.Text) / 1000);
    //
    //        if (Armed)
    //        {
    //            transientOptionsChanged();
    //        }
    //    }
    //}

    //private void udMVA0_ValueChanged(object sender, EventArgs e)
    //{
    //    if (udMVA0.ReadOnly != true)
    //    {
    //        udIO.Value = Convert.ToDecimal(Convert.ToDouble(udMVA0.Value) / (Math.Sqrt(3) * Convert.ToDouble(comboBoxTransVolt.Text)) * 1000);
    //
    //        if (Armed)
    //        {
    //            transientOptionsChanged();
    //        }
    //    }
    //}

    //private void buttonInitA_Click(object sender, EventArgs e)
    //{
    //    udIO.ReadOnly = false;
    //    udIO.Enabled = true;
    //    udMVA0.ReadOnly = true;
    //    udMVA0.Enabled = false;
    //    buttonInitA.Enabled = false;
    //    buttonInitMVA.Enabled = true;
    //}

    //private void buttonInitMVA_Click(object sender, EventArgs e)
    //{
    //    udMVA0.ReadOnly = false;
    //    udMVA0.Enabled = true;
    //    udIO.ReadOnly = true;
    //    udIO.Enabled = false;
    //    buttonInitA.Enabled = true;
    //    buttonInitMVA.Enabled = false;
    //}

    //private void buttonTakeSnap_Click(object sender, EventArgs e)
    //{
    //    bool verbose0 = Verbose;
    //    Verbose = true;
    //
    //    if (ansLineName == "")
    //    {
    //        datesArray[datesArray.Length - 1] = Date.Value;
    //
    //        //ambientsArray[i][0][0]
    //        //ambientsArray[i][j][0]
    //        //ambientsArray[i][j][1]
    //        ambientsArray[0] = new decimal[2][];
    //        ambientsArray[0][0] = new decimal[1];
    //        ambientsArray[0][1] = new decimal[2];
    //        ambientsArray[0][0][0] = Math.Ceiling(AmbientTemperatureDegF.Value);
    //        ambientsArray[0][1][0] = Math.Ceiling(udMaxTime.Value);
    //        ambientsArray[0][1][1] = Math.Floor(Convert.ToDecimal(textBoxTransMVA.Text);
    //
    //        if (string.Equals(ansLineName, ""))
    //        {
    //            return;
    //        }
    //
    //        enableDisableFields(false);
    //
    //        if (MessageBox.Show("Is this an all day rating?", "Day Span", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
    //        {
    //            Time.Value = new DateTime(Date.Value.Year, Date.Value.Month, Date.Value.Day, 12, 0, 0);
    //            Time.Enabled = false;
    //            toolStripStatusLabel1.Text = "In all-day Snapshot mode. Only Ambient Temp, Date and Max Time can be adjusted until Snapshot mode is cleared.";
    //        }
    //        else
    //        {
    //            Date.Enabled = false;
    //            toolStripStatusLabel1.Text = "In Snapshot mode. Only Ambient Temp and Max Time can be adjusted until Snapshot mode is cleared.";
    //        }
    //    }
    //
    //    for (int i = 0; i < datesArray.Length; i++)
    //    {
    //        if (datesArray[i] == Date.Value)
    //        {
    //            Array.Resize(ref datesArray, datesArray.Length + 1);
    //            datesArray[datesArray.Length - 1] = Date.Value;
    //        }
    //    }
    //
    //    Array.Sort(datesArray);
    //
    //    DateTime userDate = Date.Value;
    //
    //    bool valueExists = false;
    //    int ambientLoc;
    //
    //    for (ambientLoc = 0; ambientLoc < ambientsArray.Length; ambientLoc++)
    //    {
    //        if (Math.Ceiling(AmbientTemperatureDegF.Value) == ambientsArray[ambientLoc][0][0])
    //        {
    //            valueExists = true;
    //            break;
    //        }
    //    }
    //
    //    if (!valueExists)
    //    {
    //        Array.Resize(ref ambientsArray, ambientsArray.Length + 1);
    //        ambientsArray[ambientsArray.Length - 1] = new decimal[2][];
    //        ambientsArray[ambientsArray.Length - 1][0] = new decimal[1];
    //        ambientsArray[ambientsArray.Length - 1][1] = new decimal[2];
    //        ambientsArray[ambientsArray.Length - 1][0][0] = Math.Ceiling(AmbientTemperatureDegF.Value);
    //        ambientsArray[ambientsArray.Length - 1][ambientsArray[ambientsArray.Length - 1].Length - 1][0] = Math.Ceiling(udMaxTime.Value);
    //        ambientsArray[ambientsArray.Length - 1][ambientsArray[ambientsArray.Length - 1].Length - 1][1] = Math.Floor(Convert.ToDecimal(textBoxTransMVA.Text));
    //    }
    //
    //    valueExists = false;
    //
    //    for (int j = 1; j < ambientsArray[ambientLoc].Length; j++)
    //    {
    //        if (Math.Ceiling(udMaxTime.Value) == ambientsArray[ambientLoc][j][0])
    //        {
    //            valueExists = true;
    //            findMinMaxMVA(ambientLoc);
    //        }
    //    }
    //
    //    if (!valueExists)
    //    {
    //        Array.Resize(ref ambientsArray[ambientLoc], ambientsArray[ambientLoc].Length + 1);
    //        ambientsArray[ambientLoc][ambientsArray[ambientLoc].Length - 1] = new decimal[2];
    //        ambientsArray[ambientLoc][ambientsArray[ambientLoc].Length - 1][0] = Math.Ceiling(udMaxTime.Value);
    //        findMinMaxMVA(ambientLoc);
    //    }
    //
    //    if (datesArray.Length > 1)
    //    {
    //        Output("Snapshot " + snapshotCount++ + " taken. Date range now: " +
    //            datesArray[0].Month + "/" + datesArray[0].Day + "/" + datesArray[0].Year + " - " +
    //            datesArray[datesArray.Length - 1].Month + "/" +
    //            datesArray[datesArray.Length - 1].Day + "/" +
    //            datesArray[datesArray.Length - 1].Year + " on " + ansLineName);
    //    }
    //    else
    //    {
    //        Output("Snapshot " + snapshotCount++ + " taken for Date: " +
    //            Date.Value.Month + "/" + Date.Value.Day + "/" + Date.Value.Year +
    //            " on " + ansLineName +
    //            " with air temp " + AmbientTemperatureDegF.Value + " degF");
    //    }
    //
    //    Date.Value = userDate;
    //    Verbose = verbose0;
    //}

    //private void findMinMaxMVA(int ambientLoc)
    //{
    //    AutoVerbose = true;
    //    Date.Value = datesArray[0];
    //    //weatherDataChanged(); // recalac all values based on new Date.Value // prob not necessary to call this since Date.Value is onChange
    //    ambientsArray[ambientLoc][ambientsArray[ambientLoc].Length - 1][1] = Math.Floor(Convert.ToDecimal(textBoxTransMVA.Text));
    //
    //    for (int x = 0; x < datesArray[datesArray.Length - 1].Subtract(datesArray[0]).Days; x++)
    //    {
    //        Date.Value = Date.Value.AddDays(1);
    //        //weatherDataChanged(); // recalc all values based on new Date.Value // prob not necessary to call this since Date.Value is onChange
    //
    //        if (ambientsArray[ambientLoc][ambientsArray[ambientLoc].Length - 1][1] > Math.Floor(Convert.ToDecimal(textBoxTransMVA.Text)))
    //        {
    //            ambientsArray[ambientLoc][ambientsArray[ambientLoc].Length - 1][1] = Math.Floor(Convert.ToDecimal(textBoxTransMVA.Text));
    //        }
    //    }
    //
    //    AutoVerbose = false;
    //}

    //private void buttonClearSnap_Click(object sender, EventArgs e)
    //{
    //    bool verbose0 = Verbose;
    //    Verbose = true;
    //
    //    clearSnap();
    //
    //    Output("Snapshots cleared");
    //    Verbose = verbose0;
    //}

    //private void clearSnap()
    //{
    //    emailSubject = "";
    //    emailBody = "";
    //    emailDate = "";
    //    ansLineName = "";
    //    datesArray = new DateTime[1];
    //    ambientsArray = new decimal[1][][];
    //    ambientsArray[0] = new decimal[1][];
    //    ambientsArray[0][0] = new decimal[1];
    //    snapshotCount = 1;
    //
    //    Time.Enabled = true;
    //    Date.Enabled = true;
    //    enableDisableFields(true);
    //    toolStripStatusLabel1.Text = "";
    //}

    //private void enableDisableFields(Boolean trueOrFalse)
    //{
    //    createRatingsEmailToolStripMenuItem.Enabled = !trueOrFalse;
    //    draftWordDocToolStripMenuItem.Enabled = !trueOrFalse;
    //    cbCond.Enabled = trueOrFalse;
    //    T1.Enabled = trueOrFalse;
    //    T2.Enabled = trueOrFalse;
    //    R1.Enabled = trueOrFalse;
    //    R2.Enabled = trueOrFalse;
    //    HeatCapacity.Enabled = trueOrFalse;
    //    Diameter.Enabled = trueOrFalse;
    //    Absorptivity.Enabled = trueOrFalse;
    //    Emissivity.Enabled = trueOrFalse;
    //    chkUseAge.Enabled = trueOrFalse;
    //    udAge.Enabled = trueOrFalse;
    //    HeightSeaLevel.Enabled = trueOrFalse;
    //    Latitude.Enabled = trueOrFalse;
    //    LineAzimuth.Enabled = trueOrFalse;
    //    MaxTemperature.Enabled = trueOrFalse;
    //    WindVelocity.Enabled = trueOrFalse;
    //    LineWindAngle.Enabled = trueOrFalse;
    //    radClear.Enabled = trueOrFalse;
    //    radIndustrial.Enabled = trueOrFalse;
    //    comboBoxTransVolt.Enabled = trueOrFalse;
    //    udIO.Enabled = trueOrFalse;
    //    udMVA0.Enabled = trueOrFalse;
    //    buttonInitA.Enabled = trueOrFalse;
    //    buttonInitMVA.Enabled = trueOrFalse;
    //    radTau.Enabled = trueOrFalse;
    //    radSim.Enabled = trueOrFalse;
    //}

    //private void createRatingsEmailToolStripMenuItem_Click(object sender, EventArgs e)
    //{
    //    emailDate = "Date: " + Date.Value.Month + "/" + Date.Value.Day + "/" + Date.Value.Year;
    //    emailSubject = "Transient rating for " + ansLineName + " for " + datesArray[0].Month + "-" + datesArray[0].Day + "-" + datesArray[0].Year;
    //    emailBody = "<html><body>Transient rating data for the <b>" + ansLineName + "</b> line:</title></head><br><br>Conductor: <b>" + cbCond.Text + "</b><br><br>";
    //
    //    if (datesArray.Length > 1)
    //    {
    //        emailSubject += " - " + datesArray[datesArray.Length - 1].Month + "-" +
    //                        datesArray[datesArray.Length - 1].Day + "-" +
    //                        datesArray[datesArray.Length - 1].Year;
    //
    //        emailDate = "Date: " + datesArray[0].Month + "/" +
    //                    datesArray[0].Day + "/" +
    //                    datesArray[0].Year + " - " +
    //                    datesArray[datesArray.Length - 1].Month + "/" +
    //                    datesArray[datesArray.Length - 1].Day + "/" +
    //                    datesArray[datesArray.Length - 1].Year;
    //    }
    //
    //    emailBody += emailDate;
    //
    //    emailBody += "<br><br>Preloading: <b>" + Math.Floor(udIO.Value) +
    //                 " Amps (<u>" + Math.Floor(udMVA0.Value) +
    //                 " MVA</u> at " + comboBoxTransVolt.Text + " kV)</b><br><br><br>";
    //
    //    for (int i = 0; i < ambientsArray.Length; i++)
    //    {
    //        emailBody += "<br><br>Ambient air temperature: <b>" +
    //                     ambientsArray[i][0][0] + " degF</b><br><br>";
    //
    //        for (int j = 1; j < ambientsArray[i].Length; j++)
    //        {
    //            emailBody += "<b>" + ambientsArray[i][j][0] +
    //                         " minute</b> transient rating: <b><u>" +
    //                         ambientsArray[i][j][1] +
    //                         " MVA</b></u> at " +
    //                         comboBoxTransVolt.Text + " kV<br><br>";
    //        }
    //    }
    //}

    //private void AmbientTemperature_ValueChanged(object sender, EventArgs e)
    //{
    //    if (AmbientTemperature.ReadOnly != true)
    //    {
    //        try
    //        {
    //            AmbientTemperatureDegF.Value = AmbientTemperature.Value * 9 / 5 + 32;
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message);
    //            AmbientTemperature.Value = AmbientTemperature.Value - 10;
    //            AmbientTemperatureDegF.Value = AmbientTemperature.Value * 9 / 5 + 32;
    //        }
    //
    //        if (Armed)
    //        {
    //            weatherDataChanged();
    //        }
    //    }
    //}

    //private void AmbientTemperatureDegF_ValueChanged(object sender, EventArgs e)
    //{
    //    if (AmbientTemperatureDegF.ReadOnly != true)
    //    {
    //        try
    //        {
    //            AmbientTemperature.Value = (AmbientTemperatureDegF.Value - 32) * 5 / 9;
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message);
    //            AmbientTemperatureDegF.Value = AmbientTemperatureDegF.Value - 10;
    //            AmbientTemperature.Value = (AmbientTemperatureDegF.Value - 32) * 5 / 9;
    //        }
    //
    //        if (Armed)
    //        {
    //            weatherDataChanged();
    //        }
    //    }
    //}

    //private void buttonDegC_Click(object sender, EventArgs e)
    //{
    //    AmbientTemperature.ReadOnly = false;
    //    AmbientTemperature.Enabled = true;
    //    AmbientTemperatureDegF.ReadOnly = true;
    //    AmbientTemperatureDegF.Enabled = false;
    //    buttonDegC.Enabled = false;
    //    buttonDegF.Enabled = true;
    //}

    //private void buttonDegF_Click(object sender, EventArgs e)
    //{
    //    AmbientTemperature.ReadOnly = true;
    //    AmbientTemperature.Enabled = false;
    //    AmbientTemperatureDegF.ReadOnly = false;
    //    AmbientTemperatureDegF.Enabled = true;
    //    buttonDegC.Enabled = true;
    //    buttonDegF.Enabled = false;
    //}
}
