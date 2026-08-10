using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class App : Application
{
    public App()
    {
        TurkishUi.ApplyCulture();
    }
}
