using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;

namespace RfaToRvtPlugin
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ConvertRfaApp : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent -= HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            e.Succeeded = true;
            try
            {
                DesignAutomationData data = e.DesignAutomationData;
                Application app = data.RevitApp;

                string inputRfaPath = "input.rfa";
                string outputRvtPath = "result.rvt";

                if (!File.Exists(inputRfaPath))
                {
                    Console.WriteLine("❌ Erreur : Fichier RFA introuvable.");
                    e.Succeeded = false;
                    return;
                }

                Console.WriteLine("⚙️ 1. Création du projet RVT...");
                Document rvtDoc = app.NewProjectDocument(UnitSystem.Metric);

                Console.WriteLine("⚙️ 2. Chargement de la famille RFA...");
                using (Transaction t = new Transaction(rvtDoc, "Load RFA"))
                {
                    t.Start();
                    rvtDoc.LoadFamily(inputRfaPath);
                    t.Commit();
                }

                Console.WriteLine("⚙️ 3. Sauvegarde en RVT...");
                SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                rvtDoc.SaveAs(outputRvtPath, saveOptions);
                rvtDoc.Close(false);

                Console.WriteLine("✅ SUCCES !");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR FATALE : {ex.Message}");
                e.Succeeded = false;
            }
        }
    }
}
