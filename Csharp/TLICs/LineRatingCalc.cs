using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using TLineCalc;

public class LineRatingCalc
{
    private double[] alphaCoefs = new double[6] { 0.288, 0.1045, -0.0077, 0.0002, -2E-06, -7E-09 };

    private double[] epsCoefs = new double[7] { 0.5332, 0.167, -0.0297, 0.0026, -0.0001, 3E-06, -3E-08 };

    private Assembly assembly;

    private TextReader reader;

    private Dictionary<string, Dictionary<string, double>> condDictionary;

    private string[] condNames;

    private double Imax_old;

    private static double foot2meter = 3.28084;

    private static double inch2m = 25.4;

    private static double mile2meter = 1609.344;

    private static int CpAl = 433;

    private static int CpCu = 192;

    private static int CpSteel = 216;

    private static int CpCarbonFiber = 369;

    private double diameter = 1.0;

    private double R1 = 1.0;

    private double R2 = 2.0;

    private double heatCapacity;

    private double T1;

    private double T2;

    public double rateA;

    public double rateB;

    public double rateC;

    private TLineThermalModel model;

    public void InitializeCalculator()
    {
        ResetModel();
        SolveSteadyStateCurrent(isSummer: true, 125.0, "");
        LoadConductors("");
    }

    private void LoadConductors(string condDataFileName)
    {
        condDictionary = new Dictionary<string, Dictionary<string, double>>();
        try
        {
            assembly = Assembly.GetExecutingAssembly();
            reader = new StreamReader(Application.StartupPath + "\\Resources\\ConductorData.txt");
            if (condDataFileName != "")
            {
                reader = new StreamReader(condDataFileName);
            }

            string[] array = null;
            string text = reader.ReadLine();
            array = text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            int num5 = 0;
            int num6 = 0;
            int num7 = 0;
            int num8 = 0;

            for (int i = 0; i < array.Length; i++)
            {
                switch (array[i].ToUpper())
                {
                    case "CODE_NAME":
                        num = i;
                        break;
                    case "TYPE":
                        num2 = i;
                        break;
                    case "SIZE":
                        num3 = i;
                        break;
                    case "OD_IN":
                        num4 = i;
                        break;
                    case "LBS_KFT_OUTER":
                        num5 = i;
                        break;
                    case "LBS_KFT_INNER":
                        num6 = i;
                        break;
                    case "R25":
                        num7 = i;
                        break;
                    case "R75":
                        num8 = i;
                        break;
                }
            }

            double num9 = 0.0;
            string text2 = "";
            text = reader.ReadLine();
            Dictionary<string, double> dictionary = null;

            while (text.Trim().Length > 0)
            {
                array = text.Split(',');
                string text3 = array[num];
                string text4 = array[num2];
                string text5 = array[num3];

                text2 = (text5.Equals("0") ? (text3 + " (" + text4 + ")") : (text5 + " " + text4 + " (" + text3 + ")"));

                double num10 = Convert.ToDouble(array[num7]);
                num9 = Convert.ToDouble(array[num8]);
                double num11 = Convert.ToDouble(array[num4]);
                double num12 = Convert.ToDouble(array[num5]);
                double num13 = Convert.ToDouble(array[num6]);

                if (num9 > 999.9)
                {
                    num9 = num10 * 1.202;
                }

                double num14 =
                    (text4.ToUpper().Equals("CU")) ? (num12 / 1000.0 * (double)CpCu) :
                    ((text4.ToUpper().Equals("ACCC")) ? (num12 / 1000.0 * (double)CpAl + num13 / 1000.0 * (double)CpCarbonFiber) :
                    (num12 / 1000.0 * (double)CpAl + num13 / 1000.0 * (double)CpSteel));

                num11 *= inch2m;
                num10 /= mile2meter;
                num9 /= mile2meter;
                num14 *= foot2meter;

                dictionary = new Dictionary<string, double>();
                dictionary.Add("R25", num10);
                dictionary.Add("R75", num9);
                dictionary.Add("D", num11);
                dictionary.Add("HeatCap", num14);

                condDictionary.Add(text2.ToLower(), dictionary);

                text = reader.ReadLine();
                if (text == null)
                {
                    break;
                }
            }

            reader.Close();
            reader = null;
            condNames = new string[condDictionary.Keys.Count];
            condDictionary.Keys.CopyTo(condNames, 0);
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
        model = new TLineThermalModel();
        try
        {
            T1 = model.TLow;
            T2 = model.THigh;
        }
        catch (FormatException ex)
        {
            MessageBox.Show("An input field's value is in the incorrect format. " + ex.Message,
                "Format Exception", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
        catch (Exception ex2)
        {
            MessageBox.Show("An unknown exception has occured. " + ex2.Message,
                "Unknown Exception", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }

        SolveSteadyStateCurrent(isSummer: true, 125.0, "");
    }

    public string selectConductorSolve(string condName, bool isSummer, double maxTemperature)
    {
        string text = condName;
        string bundledOrCondType = "";
        try
        {
            if (text.Substring(0, 1).ToLower().Equals("b"))
            {
                text = text.Substring(1, text.Length - 1);
                bundledOrCondType = "b";
            }
            else if (text.Substring(0, 2).ToLower().Equals("tb"))
            {
                text = text.Substring(2, text.Length - 2);
                bundledOrCondType = "tb";
            }
            else if (text.Substring(0, 2).ToLower().Equals("hy"))
            {
                text = text.Substring(2, text.Length - 2);
                bundledOrCondType = "hy";
            }
            else if (text.Contains("accc"))
            {
                bundledOrCondType = "accc";
            }
            else if (text.Contains("accr"))
            {
                bundledOrCondType = "accr";
            }
        }

        catch
        {
        }

        try
        {
            diameter = condDictionary[text]["D"];
            R1 = condDictionary[text]["R25"];
            R2 = condDictionary[text]["R75"];
            heatCapacity = condDictionary[text]["HeatCap"];
            SolveSteadyStateCurrent(isSummer, maxTemperature, bundledOrCondType);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "";
    }

    private void SolveSteadyStateCurrent(bool isSummer, double maxTemperature, string bundledOrCondType)
    {
        double num = 94.0;
        double num2 = 100.0;

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
                model.AmbientTemperature = 40.0;
                model.Month = 7.0;
            }
            else
            {
                model.AmbientTemperature = 26.7;
                model.Month = 3.0;
            }

            model.DayOfMonth = 1.0;
            model.Hour = 12.0;
            model.Height = 152.0;
            model.Latitude = 32.234;
            model.LineAzimuth = 0.0;
            model.LineWindAngle = 45.0;

            switch (bundledOrCondType)
            {
                case "hy":
                    maxTemperature += 25.0;
                    num = 119.0;
                    num2 = 125.0;
                    break;

                case "accc":
                    num = maxTemperature;
                    num2 = maxTemperature;

                    if (maxTemperature > 180.0)
                    {
                        num = 180.0;
                    }

                    if (maxTemperature > 200.0)
                    {
                        num2 = 200.0;
                    }
                    break;

                case "accr":
                    num = maxTemperature;
                    num2 = maxTemperature;

                    if (maxTemperature > 210.0)
                    {
                        num = 210.0;
                    }

                    if (maxTemperature > 240.0)
                    {
                        num2 = 240.0;
                    }
                    break;
            }

            model.MaxTemperature = maxTemperature;
            model.WindVelocity = 0.61;
            model.IsClearAtmosphere = true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show("An input field's value is in the incorrect format. " + ex.Message,
                "Format Exception", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
        catch (Exception ex2)
        {
            MessageBox.Show("An unknown exception has occured. " + ex2.Message,
                "Unknown Exception", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }

        try
        {
            model.SolveSteadyState();
            rateC = 0.0;

            if (model.Imax != double.NaN)
            {
                if (bundledOrCondType == "b")
                {
                    rateC = model.Imax * 2.0;
                }
                else if (bundledOrCondType == "tb")
                {
                    rateC = model.Imax * 3.0;
                }
                else
                {
                    rateC = model.Imax;
                }
            }

            rateB = rateC;

            if (maxTemperature > num2)
            {
                model.MaxTemperature = num2;
                model.SolveSteadyState();

                if (model.Imax != double.NaN)
                {
                    if (bundledOrCondType == "b")
                    {
                        rateB = model.Imax * 2.0;
                    }
                    else if (bundledOrCondType == "tb")
                    {
                        rateB = model.Imax * 3.0;
                    }
                    else
                    {
                        rateB = model.Imax;
                    }
                }
            }

            rateA = rateB;

            if (!(maxTemperature > num))
            {
                return;
            }

            model.MaxTemperature = num;
            model.SolveSteadyState();

            if (model.Imax != double.NaN)
            {
                if (bundledOrCondType == "b")
                {
                    rateA = model.Imax * 2.0;
                }
                else if (bundledOrCondType == "tb")
                {
                    rateA = model.Imax * 3.0;
                }
                else
                {
                    rateA = model.Imax;
                }
            }
        }
        catch (Exception ex3)
        {
            MessageBox.Show(
                "An exception has occured while solving for steady-state current. " + ex3.Message,
                "Calculation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Hand);
        }
    }
}