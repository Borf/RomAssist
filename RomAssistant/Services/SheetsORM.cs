using Discord.Net;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Services;

public class RowData<T> : List<T>
{
    public List<T> originalValues { get; init; } = new();
    public Dictionary<string, int> columnIndices { get; set; } = new();
    public RowData(List<T> items) : base(items)
    {
    }
}
public class SheetsORM
{
    private SheetsService sheetsService;
    private ILogger<SheetsORM> logger;
   
    public SheetsORM(SheetsService sheetsService, ILogger<SheetsORM> logger)
    {
        this.sheetsService = sheetsService;
        this.logger = logger;
    }

    public RowData<T> Get<T>(string sheetId, string sheetName, bool forUpdate = false) where T : new()
    {
        var dataType = typeof(T);
        List<T> result = new();
        var key = dataType.GetProperties().First(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "KeyAttribute"));

        var sheets = sheetsService.Spreadsheets.Get(sheetId).Execute().Sheets;
        var tab = sheets.FirstOrDefault(s => s.Properties.Title.ToLower() == sheetName.ToLower());
        if (tab == null)
        {
            //throw new Exception("This sheet does not exist. Sheets are " + string.Join(",", sheets.Select(s => s.Properties.Title)));
            return new RowData<T>(result);
        }
        var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tab.Properties.Title}!A:Z").Execute();
        if (sheetData.Values == null)
        {
            return new RowData<T>(result);
        }

        for (int i = sheetData.Values.Count - 1; i >= 0; i--)
            if (sheetData.Values[i].All(v => v == null || string.IsNullOrWhiteSpace(v.ToString())))
            {
                sheetData.Values.RemoveAt(i);
                break;
            }
        var empty = new T();
        for (int i = 1; i < sheetData.Values.Count; i++)
        {
            var row = sheetData.Values[i];
            if (i == 0)
            {
                // Header row
                continue;
            }
            if (row.Count == 0)
                continue;
            var obj = Activator.CreateInstance<T>();
            var properties = dataType.GetProperties();
            for (int j = 0; j < properties.Length; j++)
            {
                var prop = properties[j];
                if (j >= row.Count)
                    break;
                var columnIndex = sheetData.Values[0].IndexOf(prop.Name); //TODO: do better matching
                if (columnIndex >= row.Count)
                {
                    logger.LogWarning("Property {Property} not found in sheet {Sheet} in headers: {headers}", prop.Name, sheetName, string.Join(", ", sheetData.Values[0]));
                    continue;
                }
                var cellValue = row[columnIndex]?.ToString();
                if (cellValue == null)
                {
                    logger.LogWarning("No value for {Property} on row {row}", prop.Name, j + 1);
                    continue;
                }
                if (prop.PropertyType == typeof(string))
                    prop.SetValue(obj, cellValue);
                else if (prop.PropertyType == typeof(int) && int.TryParse(cellValue, out int intValue))
                    prop.SetValue(obj, intValue);
                else if (prop.PropertyType == typeof(double) && double.TryParse(cellValue, out double doubleValue))
                    prop.SetValue(obj, doubleValue);
                else if (prop.PropertyType == typeof(ulong))
                {
                    if (ulong.TryParse(cellValue, out ulong ulongValue))
                        prop.SetValue(obj, ulongValue);
                }
                else if (prop.PropertyType == typeof(bool) && bool.TryParse(cellValue, out bool boolValue))
                    prop.SetValue(obj, boolValue);
                else if (prop.PropertyType == typeof(DateTimeOffset?))
                {
                    if (DateTimeOffset.TryParse(cellValue, out DateTimeOffset nulltime))
                        prop.SetValue(obj, nulltime);
                }
                else if (prop.PropertyType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(cellValue, out DateTimeOffset time))
                    prop.SetValue(obj, time);
                else
                    logger.LogError("Unsupported property type {PropertyType} for property {Property}", prop.PropertyType, prop.Name);
                // Add more type conversions as needed
            }
            if(key.GetValue(obj) == key.GetValue(empty))
                continue; //skip empty keys
            result.Add(obj);
        }

        if (forUpdate)
        {
            return new RowData<T>(result)
            {
                originalValues = result.Select(ShallowClone).ToList(),
            };
        }
        return new RowData<T>(result);
    }
    public void Update<T>(string sheetId, string sheetName, RowData<T> rows)
    {
        if (rows.originalValues == null)
            throw new InvalidOperationException("RowData was not loaded for update.");

        var dataType = typeof(T);

        // Get sheet + headers
        var sheet = sheetsService.Spreadsheets.Get(sheetId).Execute().Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);
        if(sheet == null) //create sheet
        {
            sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
            {
                Requests = new List<Request>() { new() { AddSheet = new AddSheetRequest() { Properties = new SheetProperties() { Title = sheetName } } } }
            }, sheetId).Execute();
            sheet = sheetsService.Spreadsheets.Get(sheetId).Execute().Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);
        }
        if (rows.columnIndices.Count == 0)
        {
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{sheetName}!A1:Z1").Execute(); //TODO: dynamic range
            if (sheetData.Values == null)
                sheetData.Values = new List<IList<object>>();
            if (sheetData.Values.Count == 0)
            {
                int colId = 0;
                var headerUpdates = new List<ValueRange>();
                sheetData.Values.Add(new List<object>()); // Ensure at least one row exists
                foreach (var prop in dataType.GetProperties())
                {
                    if (!prop.CanRead) continue;

                    headerUpdates.Add(new ValueRange
                    {
                        Range = $"{sheetName}!{ColumnIndexToLetter(colId)}1",
                        Values = new List<IList<object>>
                        {
                            new List<object> { prop.Name }
                        }
                    });
                    sheetData.Values[0].Add(prop.Name);
                    colId++;
                }
                sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest
                {
                    ValueInputOption = "USER_ENTERED",
                    Data = headerUpdates
                }, sheetId).Execute();
            }
            var headers = sheetData.Values[0].Select(h => h.ToString()!).ToList();
            rows.columnIndices = headers.Select((h, i) => new { h, i }).ToDictionary(x => x.h, x => x.i);
        }


        var key = dataType.GetProperties().First(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "KeyAttribute"));



        var updates = new List<ValueRange>();
        int newIndex = rows.originalValues.Count+2; //header + starting index of 1
        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var current = rows[rowIdx];
            var original = rows.originalValues.FirstOrDefault(r => key.GetValue(r)?.Equals(key.GetValue(current)) ?? false);

            int sheetRowIndex = newIndex;
            if (original != null)
                rows.originalValues.IndexOf(original);
            else
                newIndex++;

            foreach (var prop in dataType.GetProperties())
            {
                if (!prop.CanRead) continue;
                if (!rows.columnIndices.TryGetValue(prop.Name, out int colIndex))
                    continue;

                var newValue = prop.GetValue(current);
                var oldValue = original != null ? prop?.GetValue(original) : null;

                if (Equals(oldValue, newValue))
                    continue;

                string columnLetter = ColumnIndexToLetter(colIndex);

                updates.Add(new ValueRange
                {
                    Range = $"{sheetName}!{columnLetter}{sheetRowIndex}",
                    Values = new List<IList<object>>
            {
                new List<object> { (prop.PropertyType == typeof(ulong) ? "'" : "") + newValue ?? "" }
            }
                });
            }

            if(updates.Count > 1000)
            {
                sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest {
                        ValueInputOption = "USER_ENTERED",
                        Data = updates
                }, sheetId).Execute();
                updates.Clear();
            }
        }

        if (updates.Count == 0)
            return;

        sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest() { Requests = [new Request { RepeatCell = new RepeatCellRequest
        {
            Range = new GridRange
            {
                SheetId = sheet.Properties.SheetId,   // int
                StartRowIndex = 0,
                EndRowIndex = rows.Count,
                StartColumnIndex = 0,
                EndColumnIndex = rows.columnIndices.Max(kv => kv.Value)
            },
            Cell = new CellData
            {
                UserEnteredFormat = new CellFormat
                {
                    WrapStrategy = "CLIP"
                }
            },
            Fields = "userEnteredFormat.wrapStrategy"
        }
        }] }, sheetId)
            .Execute();

        
        var batchRequest = new BatchUpdateValuesRequest
        {
            ValueInputOption = "USER_ENTERED",
            Data = updates
        };

        sheetsService.Spreadsheets.Values
            .BatchUpdate(batchRequest, sheetId)
            .Execute();
    }



    static T ShallowClone<T>(T source) where T : new()
    {
        var clone = new T();
        foreach (var prop in typeof(T).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            prop.SetValue(clone, prop.GetValue(source));
        }
        return clone;
    }
    static string ColumnIndexToLetter(int index)
    {
        var sb = new StringBuilder();
        index++;

        while (index > 0)
        {
            index--;
            sb.Insert(0, (char)('A' + (index % 26)));
            index /= 26;
        }

        return sb.ToString();
    }
}


