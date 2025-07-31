using Database.Models;
using System;
using System.Collections.Generic;


namespace Database.Services
{
    public interface IPrinterService
    {
        void ProcessPrinterQueue();
        void UpdatePrinterStatus(int printerId, PrinterInfo state);
        IEnumerable<PrinterInfo> GetActivePrinters();
        void HandleStuckPrinters();
    }

    public class PrinterService : IPrinterService
    {
        private readonly IPrinterRepository _repository;
        private readonly IPrinterStateManager _stateManager;
        private readonly IPrinterCommandService _commandService;
        private readonly IPrinterQueryService _queryService;

        public PrinterService(
            IPrinterRepository repository,
            IPrinterStateManager stateManager,
            IPrinterCommandService commandService,
            IPrinterQueryService queryService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        public void ProcessPrinterQueue()
        {
            try
            {
                IEnumerable<PrinterInfo> activePrinters = _queryService.GetActivePrinters();

                foreach (PrinterInfo printer in activePrinters)
                {
                    ProcessSinglePrinter(printer);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при обработке очереди принтеров", ex);
            }
        }

        private void ProcessSinglePrinter(PrinterInfo printer)
        {
            try
            {
                if (_stateManager.IsPrinterStuck(printer))
                {
                    _commandService.HandleStuckPrinter(printer.Id);
                }
                else if (_stateManager.IsStatusUpdateNeeded(printer))
                {
                    _commandService.UpdatePrinterStatus(printer.Id, printer);
                }
            }
            catch (Exception ex)
            {
                _commandService.LogError(printer.Id, ex.Message);
            }
        }

        public void UpdatePrinterStatus(int printerId, PrinterInfo state)
        {
            _commandService.UpdatePrinterStatus(printerId, state);
        }

        public IEnumerable<PrinterInfo> GetActivePrinters()
        {
            return _queryService.GetActivePrinters();
        }

        public void HandleStuckPrinters()
        {
            _commandService.HandleStuckPrinters();
        }

        internal string TryReserveAvailablePrinter(string revitFilePath, string[] printerNames)
        {
            throw new NotImplementedException();
        }

        internal bool TryReserveSpecificPrinter(string printerName, string dummyFilePath)
        {
            throw new NotImplementedException();
        }

        internal bool TryReleasePrinter(string printerName)
        {
            throw new NotImplementedException();
        }

        internal int CleanupExpiredReservations()
        {
            throw new NotImplementedException();
        }
    }
}