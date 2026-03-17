using System;

namespace TLineCalc;

public class TLineThermalModel : ICloneable
{
    private static double[] QS_COEFS_CLEAR = new double[7] { -42.2391, 63.8044, -1.922, 0.0347, -0.000361, 1.94E-06, -4.80E-09 };
    private static double[] QS_COEFS_IND = new double[7] { 53.1821, 14.211, 0.6614, -0.0317, 0.000547, -4.34E-06, 1.32E-08 };
    private static double[] K_SOLAR_COEFS = new double[3] { 1.0, 0.000115, -1.11E-08 };

    private static double DEF_HE = 150.0;
    private static double DEF_LAT = 32.2;
    private static double DEF_TMAX = 125.0;
    private static double DEF_D = 28.14;
    private static double DEF_T1 = 25.0;
    private static double DEF_T2 = 75.0;
    private static double DEF_R1 = 7.27E-05;
    private static double DEF_R2 = 8.74E-05;
    private static double DEF_ALPHA = 0.91;
    private static double DEF_EPS = 0.91;
    private static double DEF_ZL = 0.0;
    private static double DEF_VW = 0.61;
    private static double DEF_TA = 40.0;
    private static double DEF_PHI = 45.0;

    private static double DEF_MONTH = 7.0;
    private static double DEF_DAY = 1.0;
    private static double DEF_HOUR = 12.0;
    private static double DEF_MCP = 1309.0;
    private static double DEF_I0 = 800.0;
    private static double DEF_tMAX = 1800.0;
    private static bool DEF_CLEAR = true;

    private static double DEG2RAD = Math.PI / 180.0;
    private static double RAD2DEG = 180.0 / Math.PI;

    private double m_Aprime;
    private double m_C;
    private double m_D;
    private double m_Hc;
    private double m_He;
    private double m_I;
    private double m_Ii;
    private double m_If;
    private double m_Kangle;
    private double m_Ksolar;
    private double m_kf;

    private double m_Lat;
    private double m_mCp;
    private double m_N;
    private double m_qcn;
    private double m_qc1;
    private double m_qc2;
    private double m_qc;
    private double m_qr;
    private double m_qs;
    private double m_Qs;
    private double m_Qse;
    private double m_RTc;
    private double m_Ta;
    private double m_Tc;
    private double m_Tf;
    private double m_Ti;
    private double m_Tfilm;
    private double m_Tlow;
    private double m_Thigh;
    private double m_Vw;
    private double m_Zc;

    private double m_ZL;
    private double m_dt;
    private double m_dTc;
    private double m_alpha;
    private double m_delta;
    private double m_eps;
    private double m_tau;
    private double m_phi;
    private double m_beta;
    private double m_rhof;
    private double m_theta;
    private double m_muf;
    private double m_omega;
    private double m_chi;
    private double m_Rlow;
    private double m_Rhigh;
    private double m_month;
    private double m_day;
    private double m_hour;
    private double m_tmax;

    private double[] m_transX;
    private double[] m_transY;

    private bool m_isClear;
    private bool m_isDirty;

    public double Diameter
    {
        get
        {
            return m_D;
        }
        set
        {
            if (value > 0.0)
            {
                m_D = value;
                return;
            }
            throw new ArgumentException("D variable must be > 0");
        }
    }

    public double Height
    {
        get
        {
            return m_He;
        }
        set
        {
            m_He = value;
        }
    }

    public double Latitude
    {
        get
        {
            return m_Lat;
        }
        set
        {
            if (value >= -90.0 && value <= 90.0)
            {
                m_Lat = value;
                return;
            }
            throw new ArgumentException("Latitude must be >= -90 and <= 90 deg");
        }
    }

    public double MaxTemperature
    {
        get
        {
            return m_Tc;
        }
        set
        {
            m_Tc = value;
        }
    }

    public double TLow
    {
        get
        {
            return m_Tlow;
        }
        set
        {
            m_Tlow = value;
        }
    }

    public double THigh
    {
        get
        {
            return m_Thigh;
        }
        set
        {
            m_Thigh = value;
        }
    }

    public double RLow
    {
        get
        {
            return m_Rlow;
        }
        set
        {
            if (value > 0.0)
            {
                m_Rlow = value;
                return;
            }
            throw new ArgumentException("RLow variable must be > 0");
        }
    }

    public double RHigh
    {
        get
        {
            return m_Rhigh;
        }
        set
        {
            if (value > 0.0)
            {
                m_Rhigh = value;
                return;
            }
            throw new ArgumentException("RHigh variable must be > 0");
        }
    }

    public double Absorptivity
    {
        get
        {
            return m_alpha;
        }
        set
        {
            if (value >= 0.23 && value <= 0.91)
            {
                m_alpha = value;
                return;
            }
            throw new ArgumentException("Absorptivity must be >=0.23 and <=0.91");
        }
    }

    public double Emissivity
    {
        get
        {
            return m_eps;
        }
        set
        {
            if (value >= 0.23 && value <= 0.91)
            {
                m_eps = value;
                return;
            }
            throw new ArgumentException("Emissivity must be >=0.23 and <=0.91");
        }
    }

    public double LineAzimuth
    {
        get
        {
            return m_ZL;
        }
        set
        {
            if (value >= 0.0 && value <= 90.0)
            {
                m_ZL = value;
                return;
            }
            throw new ArgumentException("LineAzimuth must be >=0.0 and <=90.0");
        }
    }

    public double WindVelocity
    {
        get
        {
            return m_Vw;
        }
        set
        {
            if (value > 0.0)
            {
                m_Vw = value;
                return;
            }
            throw new ArgumentException("WindVelocity variable must be > 0");
        }
    }

    public double AmbientTemperature
    {
        get
        {
            return m_Ta;
        }
        set
        {
            m_Ta = value;
        }
    }

    public double LineWindAngle
    {
        get
        {
            return m_phi;
        }
        set
        {
            if (value >= 0.0 && value <= 90.0)
            {  
                m_phi = value;
                m_beta = 90 - value;
                return;
            }
            throw new ArgumentException("LineWindAngle must be >= 0.0 and <= 90.0")
        }
    }

    public double TransientHeatCapacity
    {
        get
        {
            return m_mCp;
        }
        set
        {
            if (value > 0.0)
            {
                m_mCp = value;
                return;
            }
            throw new ArgumentException("TransientHeatCapacity must be > 0.0");
        }
    }

    public double TransientInitialCurrent
    {
        get
        {
            return m_Ii;
        }
        set
        {
            if (value > 0.0)
            {
                m_Ii = value;
                return;
            }
            throw new ArgumentException("TransientInitialCurrent variable must be > 0");
        }
    }

    public double TransientMaxTime
    {
        get
        {
            return m_tmax;
        }
        set
        {
            if (value > 0.0)
            {
                m_tmax = value;
                return;
            }
            throw new ArgumentException("TransientMaxTime variable must be > 0");
        }
    }

    public double TransientTimeStep
    {
        get
        {
            return m_dt;
        }
        set
        {
            if (value > 0.0)
            {
                m_dt = value;
                return;
            }
            throw new ArgumentException("Time Step must be > 0.0 seconds");
        }
    }

    public double Month
    {
        get
        {
            return m_month;
        }
        set
        {
            if (value > 0.0 && value < 13.0)
            {
                m_month = value;
                return;
            }
            throw new ArgumentException("Month must be number from 1 thru 12");
        }
    }

    public double DayOfMonth
    {
        get
        {
            return m_day;
        }
        set
        {
            if (value > 0.0 && value < 32.0)
            {
                m_day = value;
                return;
            }
            throw new ArgumentException("Day of Month must be number from 1 thru 31");
        }
    }

    public double Hour
    {
        get
        {
            return m_hour;
        }
        set
        {
            m_hour = value;
        }
    }

    public bool IsClearAtmosphere
    {
        get
        {
            return m_isClear;
        }
        set
        {
            m_isClear = value;
        }
    }

    public double Resistivity => m_Rlow + (m_Tc - m_Tlow) * (m_Rhigh - m_Rlow) / (m_Thigh - m_Tlow);

    public double Imax
    {
        get
        {
            if (m_isDirty)
            {
                SolveSteadyState();
            }
            return m_I;
        }
    }

    public double[] TransientTimeData => m_transX;
    public double[] TransientTemperatureData => m_transY;
    public double TransientTimeConstant => m_tau;
    public double TransientFinalCurrent => m_If;
    public double TransientFinalTemp => m_Tf;
    public double TransientInitialTemp => m_Ti;
    public double SolarHeatGain => m_qs;
    public double ConvectedHeatLoss => m_qc;
    public double RatiatedHeatLoss => m_qr;

    public TLineThermalModel()
    {
        LoadDefaults();
        SolveSteadyState();
    }

    public TLineThermalModel(
        double height,
        double latitude,
        double maxTemperature,
        double diameter,
        double tLow,
        double tHigh,
        double rLow,
        double rHigh,
        double absorptivity,
        double emmisivity,
        double lineAzimuth,
        double windVelocity,
        double ambientTemperature,
        double lineWindAngle,
        double month,
        double dayOfMonth,
        double hour,
        bool isClearAtmosphere,
        double transientHeatCapacity,
        double transientInitialCurrent,
        double transientMaxTime)
    {
        m_He = height;
        m_Lat = latitude;
        m_Tc = maxTemperature;
        m_D = diameter;
        m_Tlow = tLow;
        m_Thigh = tHigh;
        m_Rlow = rLow;
        m_Rhigh = rHigh;
        m_alpha = absorptivity;
        m_eps = emmisivity;
        m_ZL = lineAzimuth;
        m_Vw = windVelocity;
        m_Ta = ambientTemperature;
        m_phi = lineWindAngle;
        m_isClear = isClearAtmosphere;
        m_month = month;
        m_day = dayOfMonth;
        m_hour = hour;
        m_mCp = transientHeatCapacity;
        m_Ii = transientInitialCurrent;
        m_tmax = transientMaxTime;
        m_isDirty = true;
        SolveSteadyState();
    }

    public TLineThermalModel(
        double height,
        double latitude,
        double maxTemperature,
        double diameter,
        double tLow,
        double tHigh,
        double rLow,
        double rHigh,
        double absorptivity,
        double emmisivity,
        double lineAzimuth,
        double windVelocity,
        double ambientTemperature,
        double lineWindAngle,
        double month,
        double dayOfMonth,
        double hour,
        bool isClearAtmosphere)
    {
        LoadDefaults();
        m_He = height;
        m_Lat = latitude;
        m_Tc = maxTemperature;
        m_D = diameter;
        m_Tlow = tLow;
        m_Thigh = tHigh;
        m_Rlow = rLow;
        m_Rhigh = rHigh;
        m_alpha = absorptivity;
        m_eps = emmisivity;
        m_ZL = lineAzimuth;
        m_Vw = windVelocity;
        m_Ta = ambientTemperature;
        m_phi = lineWindAngle;
        m_isClear = isClearAtmosphere;
        m_month = month;
        m_day = dayOfMonth;
        m_hour = hour;
        m_isDirty = true;
        SolveSteadyState();
    }

    private void LoadDefaults()
    {
        m_He = DEF_HE;
        m_Lat = DEF_LAT;
        m_Tc = DEF_TMAX;
        m_D = DEF_D;
        TLow = DEF_T1;
        THigh = DEF_T2;
        RLow = DEF_R1;
        RHigh = DEF_R2;
        m_alpha = DEF_ALPHA;
        m_eps = DEF_EPS;
        m_ZL = DEF_ZL;
        m_Vw = DEF_VW;
        m_Ta = DEF_TA;
        m_phi = DEF_PHI;
        m_isClear = DEF_CLEAR;
        m_month = DEF_MONTH;
        m_day = DEF_DAY;
        m_hour = DEF_HOUR;
        m_mCp = DEF_MCP;
        m_Ii = DEF_I0;
        m_tmax = DEF_tMAX;
        m_isDirty = true;
    }

    public bool SolveSteadyState()
    {
        bool result = false;
        bool flag = false;

        try
        {
            m_Aprime = 1.0 / 1000.0;
            m_omega = (12.0 - m_hour) * 15.0;
            m_N = GetDayOfYear(m_month, m_day, m_hour);
            m_delta = 23.4583 * Math.Sin((284.0 - m_N) / 365.0 * 360.0 * DEG2RAD);
            m_chi = Math.Sin(m_omega * DEG2RAD) /
                    (Math.Sin(m_Lat * DEG2RAD) * Math.Cos(m_omega * DEG2RAD) -
                     Math.Cos(m_Lat * DEG2RAD) * Math.Tan(m_delta * DEG2RAD));

            if (m_chi < 0.0)
            {
                if (m_omega < 0.0)
                {
                    m_C = 180.0;
                }
                else
                {
                    m_C = 360.0;
                }
            }
            else if (m_omega < 0.0)
            {
                m_C = 0.0;
            }
            else
            {
                m_C = 180.0;
            }

            m_Hc = Math.Asin(Math.Cos(m_Lat * DEG2RAD) * Math.Cos(m_delta * DEG2RAD) * Math.Cos(m_omega * DEG2RAD) +
                             Math.Sin(m_Lat * DEG2RAD) * Math.Sin(m_delta * DEG2RAD)) * RAD2DEG;
            m_Kangle = 1.194 - Math.Cos(m_phi * DEG2RAD) + 0.194 * Math.Cos(2.0 * m_phi * DEG2RAD) +
                       0.368 * Math.Sin(2.0 * m_phi * DEG2RAD);
            m_Ksolar = EvalPoly(K_SOLAR_COEFS, m_He);
            m_Tfilm = (m_Tc + m_Ta) / 2.0;
            m_kf = 0.02424 + 7.477E-05 * m_Tfilm - 4.407E-09 * m_Tfilm * m_Tfilm;
            m_rhof = (1.293 - 0.0001525 * m_He + 6.379E-09 * m_He * m_He) / (1.0 + 0.00367 * m_Tfilm);
            m_muf = 1.458E-06 * Math.Pow((m_Tfilm + 273.0), 1.5) / (m_Tfilm + 383.4);
            m_qcn = 0.0205 * Math.Pow(m_rhof, 0.5) * Math.Pow(m_D, 0.75) * Math.Pow(m_Tc - m_Ta, 1.25);
            m_qc1 = (1.01 + 0.0372 * Math.Pow(m_D * m_rhof * m_Vw / m_muf, 0.52)) * m_kf * m_Kangle * (m_Tc - m_Ta);
            m_qc2 = 0.0119 * Math.Pow(m_D * m_rhof * m_Vw / m_muf, 0.6) * m_kf * m_Kangle * (m_Tc - m_Ta);
            m_qc = Math.Max(m_qc1, m_qc2);
            m_qr = 0.0178 * m_D * m_eps *
                   (Math.Pow((m_Tc + 273.0) / 100.0, 4.0) - Math.Pow((m_Ta + 273.0) / 100.0, 4.0));
            m_Zc = m_C + Math.Atan(m_chi) * RAD2DEG;
            m_theta = Math.Acos(Math.Cos(m_Hc * DEG2RAD) * Math.Cos((m_Zc - m_ZL) * DEG2RAD)) * RAD2DEG;

            if (m_isClear)
            {
                m_Qs = EvalPoly(QS_COEFS_CLEAR, m_Hc);
            }
            else
            {
                m_Qs = EvalPoly(QS_COEFS_IND, m_Hc);
            }

            m_RTc = (m_Rhigh - m_Rlow) / (m_Thigh - m_Tlow) * (m_Tc - m_Tlow) + RLow;
            m_Qse = m_Ksolar * m_Qs;
            m_qs = m_alpha * m_Qse * Math.Sin(m_theta * DEG2RAD) * m_Aprime;
            m_I = Math.Sqrt((m_qc + m_qr - m_qs) / m_RTc);
        }
        catch
        {
            flag = true;
        }

        m_isDirty = false;
        if (m_I != double.NaN && m_I != double.NegativeInfinity && m_I != double.PositiveInfinity && !flag)
        {
            result = true;
        }

        return result;
    }

    public bool SolveTransientTauMethod()
    {
        bool result = false;
        bool flag = false;
        TLineThermalModel tLineThermalModel = (TLineThermalModel)Clone();
        double num = 0.0;
        double num2 = 1.0;
        double num3 = 0.1;
        int num4 = 10000;
        int num5 = 0;

        try
        {
            tLineThermalModel.m_If = tLineThermalModel.m_Ii;
            tLineThermalModel.m_Ti = TemperatureAt(m_Ii, 0.1);
            num = (tLineThermalModel.MaxTemperature - tLineThermalModel.m_Ti);
            tLineThermalModel.SolveSteadyState();
            while (num < m_Tc && num5 < num4)
            {
                tLineThermalModel.m_If += num2;
                tLineThermalModel.m_Tf = TemperatureAt(tLineThermalModel.m_If, 0.1);
                tLineThermalModel.m_tau = (tLineThermalModel.m_Tf - tLineThermalModel.m_Ti) * tLineThermalModel.m_mCp /
                    (tLineThermalModel.Resistivity * (tLineThermalModel.m_If * tLineThermalModel.m_If - tLineThermalModel.m_Ii * tLineThermalModel.m_Ii));
                num = (tLineThermalModel.MaxTemperature - tLineThermalModel.m_Ti +
                    (tLineThermalModel.m_Tf - tLineThermalModel.m_Ti) *
                    (1.0 - Math.Exp((0.0 - tLineThermalModel.m_tmax) / tLineThermalModel.m_tau)));
                tLineThermalModel.SolveSteadyState();
                num5++;
            }

            m_Ii = tLineThermalModel.m_Ii;
            m_Ti = tLineThermalModel.m_Ti;
            m_Tf = tLineThermalModel.m_Tf;
            m_If = tLineThermalModel.m_If;
            m_tau = tLineThermalModel.m_tau;
        }
        catch
        {
            flag = true;
        }

        if (Math.Abs(m_Tc - num) < num3 && !flag)
        {
            result = true;
        }
        return result;
    }

    public bool SolveTransientSimulationMethod()
    {
        bool result = false;
        bool flag = false;
        TLineThermalModel tLineThermalModel = (TLineThermalModel)Clone();
        double num = 0.0;
        double num2 = 1.0;
        int num3 = 10000;
        int num4 = 0;
        int num5 = 240;
        double num6 = -1800.0;
        double num7 = 5400.0;
        double num8 = 0.0;
        double num9 = 0.0;
        double num10 = 0.1;
        double num11 = 10000.0;

        try
        {
            m_dt = (num7 - num6) / (double)num5;
            if (m_dt <= 0.0)
            {
                SolveTransientTauMethod();
                m_dt = m_tau / 100.0;
            }

            m_Ti = TemperatureAt(m_Ii, 0.1);
            num8 = m_Ti;
            num9 = m_Ti;
            tLineThermalModel.MaxTemperature = num8;
            tLineThermalModel.SolveSteadyState();
            m_If = m_Ii;

            while (num9 <= m_Tc)
            {
                m_transX = new double[num5];
                m_transY = new double[num5];
                num8 = m_Ti;
                num = num6;

                for (int i = 0; i < num5; i++)
                {
                    m_transX[i] = num;

                    if (num < 0.0)
                    {
                        m_transY[i] = m_Ti;
                    }
                    else
                    {
                        double num12 = 1.0 / m_mCp *
                            (tLineThermalModel.Resistivity * m_If * m_If +
                             tLineThermalModel.SolarHeatGain -
                             tLineThermalModel.ConvectedHeatLoss -
                             tLineThermalModel.RatiatedHeatLoss);

                        m_dTc = num12 * m_dt;
                        num8 += m_dTc;
                        m_transY[i] = num8;
                        tLineThermalModel.MaxTemperature = num8;
                        tLineThermalModel.SolveSteadyState();

                        if (num <= m_tmax)
                        {
                            num9 = num8;
                        }
                    }

                    num += m_dt;
                }

                m_If += num2;
                num4++;

                if (num4 > num3 || m_If > num11)
                {
                    break;
                }
            }

            m_Tf = TemperatureAt(m_If, 0.1);
        }
        catch
        {
            flag = true;
        }

        if (Math.Abs(m_Tc - num8) < num10 && !flag)
        {
            result = true;
        }

        return result;
    }

    public double TemperatureAt(double current, double eps)
    {
        TLineThermalModel tLineThermalModel = (TLineThermalModel)Clone();
        double num = 10.0;
        double num2 = 0.1;
        double num3 = 10000.0;
        double num4 = 0.1;
        int num5 = 10000;
        int num6 = 0;
        double maxTemperature = tLineThermalModel.MaxTemperature;
        double num7 = maxTemperature;
        double num8 = num;
        tLineThermalModel.SolveSteadyState();
        double imax = tLineThermalModel.Imax;

        while (Math.Abs(imax - current) > eps)
        {
            if (imax < current)
            {
                while (imax < current)
                {
                    tLineThermalModel.MaxTemperature = num7;
                    tLineThermalModel.SolveSteadyState();
                    imax = tLineThermalModel.Imax;

                    if (num7 > num3)
                    {
                        break;
                    }

                    num7 += num8;
                }
            }
            else
            {
                while (imax > current)
                {
                    tLineThermalModel.MaxTemperature = num7;
                    tLineThermalModel.SolveSteadyState();
                    imax = tLineThermalModel.Imax;

                    if (num7 < num4)
                    {
                        break;
                    }

                    num7 -= num8;
                }
            }

            num6++;
            num8 *= num2;

            if (num6 > num5)
            {
                break;
            }
        }

        return tLineThermalModel.MaxTemperature;
    }

    private double GetDayOfYear(double month, double day, double hour)
    {
        double num = 0.0;
        DateTime dateTime = new DateTime(2000, 1, 1, 0, 0, 0);
        DateTime dateTime2 = new DateTime(2000, (int)month, (int)day, (int)hour, 0, 0);
        return (dateTime2 - dateTime).TotalDays;
    }

    public static double EvalPoly(double[] coef, double x)
    {
        double num = 0.0;

        for (int i = 0; i < coef.Length; i++)
        {
            num += Math.Pow(x, i) * coef[i];
        }

        return num;
    }

    public object Clone()
    {
        TLineThermalModel tLineThermalModel = new TLineThermalModel();
        tLineThermalModel.Absorptivity = Absorptivity;
        tLineThermalModel.AmbientTemperature = AmbientTemperature;
        tLineThermalModel.DayOfMonth = DayOfMonth;
        tLineThermalModel.Diameter = Diameter;
        tLineThermalModel.Emmisivity = Emmisivity;
        tLineThermalModel.Height = Height;
        tLineThermalModel.Hour = Hour;
        tLineThermalModel.IsClearAtmosphere = IsClearAtmosphere;
        tLineThermalModel.Latitude = Latitude;
        tLineThermalModel.LineAzimuth = LineAzimuth;
        tLineThermalModel.LineWindAngle = LineWindAngle;
        tLineThermalModel.MaxTemperature = MaxTemperature;
        tLineThermalModel.Month = Month;
        tLineThermalModel.RLow = RLow;
        tLineThermalModel.RHigh = RHigh;
        tLineThermalModel.TLow = TLow;
        tLineThermalModel.THigh = THigh;
        tLineThermalModel.WindVelocity = WindVelocity;
        tLineThermalModel.TransientHeatCapacity = TransientHeatCapacity;
        tLineThermalModel.TransientInitialCurrent = TransientInitialCurrent;
        tLineThermalModel.TransientMaxTime = TransientMaxTime;
        tLineThermalModel.SolveSteadyState();
        return tLineThermalModel;
    }
}