using Database.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Database.Providers
{
    /// <summary>
    /// Полностью функциональный in-memory провайдер
    /// Это рабочий провайдер, который не требует никаких внешних зависимостей
    /// Идеально подходит для разработки, тестирования и демонстрации возможностей
    /// </summary>
    public class InMemoryProvider : IDatabaseProvider
    {
        public string ProviderName => "InMemory";
        public bool SupportsRowLevelLocking => true;

        // Потокобезопасное хранилище данных в памяти
        private static readonly ConcurrentDictionary<string, PrinterState> _printers =
            new();

        // Счетчик для генерации уникальных ID
        private static int _nextId = 1;

        public IDbConnection CreateConnection(string connectionString)
        {
            // Для in-memory провайдера подключение фиктивное
            return new InMemoryConnection();
        }

        public string GetCreateTableScript()
        {
            return "-- InMemory provider: table created automatically in memory";
        }

        public string GetReservePrinterScript()
        {
            return "-- InMemory provider: reservation handled directly in memory";
        }

        /// <summary>
        /// Инициализация in-memory провайдера
        /// Создаем стандартный набор принтеров если коллекция пуста
        /// </summary>
        public void Initialize(string connectionString)
        {
            if (_printers.IsEmpty)
            {
                string[] defaultPrinters = new[]
                {
                    "PDF Writer - bioPDF",
                    "PDF24",
                    "PDFCreator",
                    "clawPDF",
                    "Adobe PDF"
                };

                foreach (string printerName in defaultPrinters)
                {
                    PrinterState printerState = new()
                    {
                        Id = _nextId++,
                        PrinterName = printerName,
                        IsAvailable = true,
                        LastUpdated = DateTime.UtcNow,
                        Version = 1
                    };

                    _ = _printers.TryAdd(printerName, printerState);
                }
            }
        }

        #region Direct memory operations

        /// <summary>
        /// Прямые операции с данными в памяти
        /// Эти методы обходят SQL и работают напрямую с ConcurrentDictionary
        /// </summary>

        public bool TryReservePrinter(string printerName, string reservedBy)
        {
            return _printers.TryUpdate(printerName,
                CreateReservedState(_printers[printerName], reservedBy),
                _printers[printerName]);
        }

        public bool ReleasePrinter(string printerName)
        {
            if (_printers.TryGetValue(printerName, out PrinterState printer))
            {
                PrinterState releasedState = new()
                {
                    Id = printer.Id,
                    PrinterName = printerName,
                    IsAvailable = true,
                    ReservedBy = null,
                    ReservedAt = null,
                    LastUpdated = DateTime.UtcNow,
                    ProcessId = null,
                    MachineName = null,
                    Version = printer.Version + 1
                };

                return _printers.TryUpdate(printerName, releasedState, printer);
            }
            return false;
        }

        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            return _printers.Values.Where(p => p.IsAvailable).ToList();
        }

        public PrinterState GetPrinter(string printerName)
        {
            _ = _printers.TryGetValue(printerName, out PrinterState printer);
            return printer;
        }

        public IEnumerable<PrinterState> GetAllPrinters()
        {
            return _printers.Values.ToList();
        }

        private static PrinterState CreateReservedState(PrinterState original, string reservedBy)
        {
            return new PrinterState
            {
                Id = original.Id,
                PrinterName = original.PrinterName,
                IsAvailable = false,
                ReservedBy = reservedBy,
                ReservedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                MachineName = Environment.MachineName,
                Version = original.Version + 1
            };
        }

        #endregion
    }

    #region Фиктивные ADO.NET классы для совместимости

    /// <summary>
    /// Эти классы нужны только для совместимости с интерфейсами ADO.NET
    /// В реальной работе InMemoryProvider использует прямые операции с памятью
    /// </summary>

    internal class InMemoryConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 30;
        public string Database => "InMemory";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction()
        {
            return new InMemoryTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return new InMemoryTransaction();
        }

        public void ChangeDatabase(string databaseName) { }
        public void Close()
        {
            State = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand()
        {
            return new InMemoryCommand();
        }

        public void Dispose() { }
        public void Open()
        {
            State = ConnectionState.Open;
        }
    }

    internal class InMemoryTransaction : IDbTransaction
    {
        public IDbConnection Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }

    internal class InMemoryCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new InMemoryParameterCollection();
        public IDbTransaction Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter()
        {
            return new InMemoryParameter();
        }

        public void Dispose() { }
        public int ExecuteNonQuery()
        {
            return 1;
        }

        public IDataReader ExecuteReader()
        {
            return new InMemoryDataReader();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            return new InMemoryDataReader();
        }

        public object ExecuteScalar()
        {
            return null;
        }

        public void Prepare() { }
    }

    internal class InMemoryParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    internal class InMemoryDataReader : IDataReader
    {
        private bool _hasData = false;

        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public int FieldCount => 4; // id, printer_name, is_available, version

        public void Close() { }
        public void Dispose() { }
        public bool GetBoolean(int i)
        {
            return i == 2 && _hasData; // is_available
        }

        public byte GetByte(int i)
        {
            return 0;
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            return 0;
        }

        public char GetChar(int i)
        {
            return '\0';
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            return 0;
        }

        public IDataReader GetData(int i)
        {
            return null;
        }

        public string GetDataTypeName(int i)
        {
            return "TEXT";
        }

        public DateTime GetDateTime(int i)
        {
            return DateTime.MinValue;
        }

        public decimal GetDecimal(int i)
        {
            return 0;
        }

        public double GetDouble(int i)
        {
            return 0;
        }

        public Type GetFieldType(int i)
        {
            return typeof(string);
        }

        public float GetFloat(int i)
        {
            return 0;
        }

        public Guid GetGuid(int i)
        {
            return Guid.Empty;
        }

        public short GetInt16(int i)
        {
            return 0;
        }

        public int GetInt32(int i)
        {
            return i == 0 ? 1 : 0; // id
        }

        public long GetInt64(int i)
        {
            return i == 3 ? 1 : 0; // version
        }

        public string GetName(int i)
        {
            return i switch
            {
                0 => "id",
                1 => "printer_name",
                2 => "is_available",
                3 => "version",
                _ => ""
            };
        }

        public int GetOrdinal(string name)
        {
            return name switch
            {
                "id" => 0,
                "printer_name" => 1,
                "is_available" => 2,
                "version" => 3,
                _ => -1
            };
        }

        public DataTable GetSchemaTable()
        {
            return null;
        }

        public string GetString(int i)
        {
            return i == 1 ? "TestPrinter" : "";
        }

        public object GetValue(int i)
        {
            return i switch
            {
                0 => 1,
                1 => "TestPrinter",
                2 => true,
                3 => 1L,
                _ => null
            };
        }

        public int GetValues(object[] values)
        {
            return 0;
        }

        public bool IsDBNull(int i)
        {
            return false;
        }

        public bool NextResult()
        {
            return false;
        }

        public bool Read()
        {
            if (!_hasData)
            {
                _hasData = true;
                return true;
            }
            return false;
        }
    }

    #endregion
}