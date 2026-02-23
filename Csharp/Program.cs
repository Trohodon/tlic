using System;
using System.Colletions.Generic;
using System.Windows.Forms;

namespace TLIC
{
    static class Program
    {
        // <summary>
        // The main entry point for the application
        // </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm())
        }
    }
}