using Database.Models;
using System;
using System.Collections.Generic;

namespace Database
{
    public interface IPrinterQueryService
    {
        IEnumerable<PrinterInfo> GetActivePrinters();
        PrinterInfo GetPrinterById(int printerId);
        IEnumerable<PrinterInfo> GetStuckPrinters(TimeSpan threshold);
    }




}
