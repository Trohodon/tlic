using System;
using System.IO;

public class Conductor_JWR
{
    private double gmr;
    private string name;
    private double r;
    private double radius;
    private double rateA;
    private double rateB;
    private double rateC;
    private TextReader reader;
    private double xC;
    private double xL;

    private bool staticConductor;
    public double GMR => gmr;
    public string Name => name;
    public double R => r;
    public double Radius => radius;
    public double RateA => rateA;
    public double RateB => rateB;
    public double RateC => rateC;
    public double XC => xC;
    public double XL => xL;

    public Conductor_JWR(string name, TextReader reader)
    {
        this.name = name;
        this.reader = reader;
        LoadFromFile();
    }

    public Conductor_JWR(string name, string filename, bool staticConductor)
    {
        this.name = name;
        reader = new StreamReader(filename);
        this.staticConductor = staticConductor;
        LoadFromFile();
    }

    public Conductor_JWR(string name, double gmr, double radius, double rateA, double rateB, double rateC, double r, double xL, double xC)
    {
        this.name = name;
        this.gmr = gmr;
        this.radius = radius;
        this.rateA = rateA;
        this.rateB = rateB;
        this.rateC = rateC;
        this.r = r;
        this.xL = xL;
        this.xC = xC;
    }

    public void LoadFromFile()
    {
        bool flag = false;
        string text = reader.ReadLine();
        text = reader.ReadLine();

        while (!flag)
        {
            string[] array = text.Split(new string[1] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length < 2)
            {
                array = text.Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (name.ToLower().Equals(array[12].ToLower()))
            {
                flag = true;
                gmr = Convert.ToDouble(array[2]);
                radius = Convert.ToDouble(array[3]);
                rateA = Convert.ToDouble(array[4]);
                rateB = Convert.ToDouble(array[5]);
                rateC = Convert.ToDouble(array[6]);
                r = Convert.ToDouble(array[7]);
                xL = Convert.ToDouble(array[8]);
                xC = Convert.ToDouble(array[9]);
            }

            text = reader.ReadLine();
            if (text == null)
            {
                flag = true;
            }
            else if (text.Equals(string.Empty))
            {
                flag = true;
            }
        }
    }
}