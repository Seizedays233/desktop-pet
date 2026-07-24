using System;
using System.Threading;
using System.Windows;

namespace OrangeCatPet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\OrangeCatPet.SmoothTestInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        app.Run(new PetWindow());
    }
}


