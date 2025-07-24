// Database/Providers/InMemoryProvider.cs
using Database.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Database.Providers
{
    /// <summary>
    /// In-memory провайдер для демонстрации принципов работы
    /// Не требует никаких внешних зависимостей, работает только в памяти
    /// Идеален для тестирования и понимания архитектуры
    /// </summary>
    public class InMemoryProvider : IDatabaseProvider
    {
        public string ProviderName => "InMemory";
        public bool SupportsRowLevelLocking => true; // В памяти можем делать что угодно

        // Храним данные в потокобезопасной коллекции
        private static readonly ConcurrentDictionary<string, PrinterState> _printers =
            new();

        public IDbConnection CreateConnection(string connectionString)
        {
            // Для in-memory провайдера подключение не нужно
            // Возвращаем фиктивное подключение
            return new InMemoryConnection();
        }

        public string GetCreateTableScript()
        {
            // Для in-memory провайдера создание таблицы не требуется
            return "-- In-memory provider doesn't need table creation";
        }

        public string GetReservePrinterScript()
        {
            // Возвращаем любой SQL - он не будет использоваться
            return "SELECT * FROM printer_states WHERE printer_name = @printerName";
        }

        public void Initialize(string connectionString)
        {
            // Инициализируем стандартные принтеры если коллекция пуста
            if (_printers.IsEmpty)
            {
                string[] defaultPrinters = new[]
                {
                    "PDF Writer - bioPDF",
                    "PDF24",
                    "PDFCreator",
                    "Adobe PDF"
                };

                foreach (string printerName in defaultPrinters)
                {
                    _ = _printers.TryAdd(printerName, new PrinterState
                    {
                        Id = _printers.Count + 1,
                        PrinterName = printerName,
                        IsAvailable = true,
                        LastUpdated = DateTime.UtcNow,
                        Version = 1
                    });
                }
            }
        }

        // Методы для прямой работы с данными в памяти
        public bool TryReservePrinter(string printerName, string reservedBy)
        {
            return _printers.TryUpdate(printerName, new PrinterState
                {
                    Id = _printers[printerName].Id,
                    PrinterName = printerName,
                    IsAvailable = false,
                    ReservedBy = reservedBy,
                    ReservedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    MachineName = Environment.MachineName,
                    Version = _printers[printerName].Version + 1
                },
                _printers[printerName]);
        }

        public bool ReleasePrinter(string printerName)
        {
            return _printers.TryGetValue(printerName, out PrinterState printer) && _printers.TryUpdate(printerName,
                    new PrinterState
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
                    },
                    printer);
        }

        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            return _printers.Values.Where(p => p.IsAvailable);
        }

        public PrinterState GetPrinter(string printerName)
        {
            _ = _printers.TryGetValue(printerName, out PrinterState printer);
            return printer;
        }
    }

    /// <summary>
    /// Фиктивное подключение для in-memory провайдера
    /// Реализует интерфейс IDbConnection, но ничего не делает
    /// </summary>
    internal class InMemoryConnection : IDbConnection
    {
        public string ConnectionString { get; set; }
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
        public string CommandText { get; set; }
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
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
            return 1; // Всегда "успешно"
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

    // Упрощенные реализации остальных интерфейсов...
    internal class InMemoryParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; }
        public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    internal class InMemoryParameterCollection : IDataParameterCollection
    {
        private readonly List<IDataParameter> _parameters = [];

        public object this[string parameterName] { get; set; }
        public object this[int index] { get; set; }
        public int Count => _parameters.Count;
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => _parameters;

        public int Add(object value)
        {
            _parameters.Add((IDataParameter)value);
            return _parameters.Count - 1;
        }

        public void Clear()
        {
            _parameters.Clear();
        }

        public bool Contains(object value)
        {
            return _parameters.Contains((IDataParameter)value);
        }

        public bool Contains(string parameterName)
        {
            return _parameters.Any(p => p.ParameterName == parameterName);
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        public int IndexOf(object value)
        {
            return _parameters.IndexOf((IDataParameter)value);
        }

        public int IndexOf(string parameterName)
        {
            return _parameters.FindIndex(p => p.ParameterName == parameterName);
        }

        public void Insert(int index, object value)
        {
            _parameters.Insert(index, (IDataParameter)value);
        }

        public void Remove(object value)
        {
            _ = _parameters.Remove((IDataParameter)value);
        }

        public void RemoveAt(int index)
        {
            _parameters.RemoveAt(index);
        }

        public void RemoveAt(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }
    }

    internal class InMemoryDataReader : IDataReader
    {
        public object this[int i] => null;
        public object this[string name] => null;
        public int Depth => 0;
        public bool IsClosed => true;
        public int RecordsAffected => 0;
        public int FieldCount => 0;

        public void Close() { }
        public void Dispose() { }
        public bool GetBoolean(int i)
        {
            return false;
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
            return "";
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
            return typeof(object);
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
            return 0;
        }

        public long GetInt64(int i)
        {
            return 0;
        }

        public string GetName(int i)
        {
            return "";
        }

        public int GetOrdinal(string name)
        {
            return 0;
        }

        public DataTable GetSchemaTable()
        {
            return null;
        }

        public string GetString(int i)
        {
            return "";
        }

        public object GetValue(int i)
        {
            return null;
        }

        public int GetValues(object[] values)
        {
            return 0;
        }

        public bool IsDBNull(int i)
        {
            return true;
        }

        public bool NextResult()
        {
            return false;
        }

        public bool Read()
        {
            return false;
        }
    }
}