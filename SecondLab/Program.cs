using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public delegate void FValues(double x, ref double y1, ref double y2);
public delegate DataItem FDI(double x);

public struct DataItem
{
    public double X { get; set; }
    public double Y1 { get; set; }
    public double Y2 { get; set; }

    public DataItem(double x, double y1, double y2)
    {
        X = x;
        Y1 = y1;
        Y2 = y2;
    }

    public override string ToString() => $"X: {X}, Y1: {Y1}, Y2: {Y2}";

    public string ToString(string format) =>
        $"X: {X.ToString(format)}, Y1: {Y1.ToString(format)}, Y2: {Y2.ToString(format)}";
}

public abstract class V1Data : IEnumerable<DataItem>
{
    public string Key { get; set; }
    public DateTime Date { get; set; }

    protected V1Data(string key, DateTime date)
    {
        Key = key;
        Date = date;
    }

    public abstract int XLength { get; }
    public abstract (double, double) MinMaxDifference { get; }
    public abstract string ToLongString(string format);

    public override string ToString() => $"Key: {Key}, Date: {Date}";

    public abstract IEnumerator<DataItem> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class V1DataList : V1Data
{
    public List<DataItem> Data { get; set; } = new();

    public V1DataList(string key, DateTime date) : base(key, date) { }

    public V1DataList(string key, DateTime date, double[] x, FDI F) : base(key, date)
    {
        foreach (var coordinate in x)
        {
            Data.Add(F(coordinate));
        }
    }

    public override int XLength => Data.Count;

    public override (double, double) MinMaxDifference
    {
        get
        {
            if (Data.Count == 0) return (0, 0);

            double min = double.MaxValue, max = double.MinValue;
            foreach (var item in Data)
            {
                double diff = Math.Abs(item.Y1 - item.Y2);
                if (diff < min) min = diff;
                if (diff > max) max = diff;
            }
            return (min, max);
        }
    }

    public static explicit operator V1DataArray(V1DataList source)
    {
        double[] xArray = source.Data.Select(item => item.X).ToArray();
        double[] valuesArray = new double[source.Data.Count * 2];

        for (int i = 0; i < source.Data.Count; i++)
        {
            valuesArray[2 * i] = source.Data[i].Y1;
            valuesArray[2 * i + 1] = source.Data[i].Y2;
        }

        return new V1DataArray(source.Key, source.Date, xArray,
            (double x, ref double y1, ref double y2) =>
            {
                int index = Array.IndexOf(xArray, x);
                if (index >= 0)
                {
                    y1 = valuesArray[2 * index];
                    y2 = valuesArray[2 * index + 1];
                }
            });
    }

    public override string ToString() => $"V1DataList: Key = {Key}, Date = {Date}, Count = {Data.Count}";

    public override string ToLongString(string format) =>
        $"{ToString()}\n{string.Join("\n", Data.Select(item => item.ToString(format)))}";

    public override IEnumerator<DataItem> GetEnumerator() => Data.GetEnumerator();
}

public class V1DataArray : V1Data
{
    public double[] XNodes { get; set; } = Array.Empty<double>();
    public double[] Values { get; set; } = Array.Empty<double>();

    public V1DataArray(string key, DateTime date) : base(key, date) { }

    public V1DataArray(string key, DateTime date, double[] x, FValues F) : base(key, date)
    {
        XNodes = x;
        Values = new double[x.Length * 2];

        for (int i = 0; i < x.Length; i++)
        {
            F(x[i], ref Values[2 * i], ref Values[2 * i + 1]);
        }
    }

    public DataItem? this[int index] =>
        index < 0 || index >= XNodes.Length ? null : new DataItem(XNodes[index], Values[2 * index], Values[2 * index + 1]);

    public override int XLength => XNodes.Length;

    public override (double, double) MinMaxDifference
    {
        get
        {
            if (Values.Length < 2) return (0, 0);

            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < XNodes.Length; i++)
            {
                double diff = Math.Abs(Values[2 * i] - Values[2 * i + 1]);
                if (diff < min) min = diff;
                if (diff > max) max = diff;
            }
            return (min, max);
        }
    }

    public override string ToString() => $"V1DataArray: Key = {Key}, Date = {Date}, Count = {XLength}";

    public override string ToLongString(string format) =>
        $"{ToString()}\n{string.Join("\n", XNodes.Select((x, i) =>
            $"X: {x.ToString(format)}, Y1: {Values[2 * i].ToString(format)}, Y2: {Values[2 * i + 1].ToString(format)}"))}";

    public override IEnumerator<DataItem> GetEnumerator()
    {
        for (int i = 0; i < XNodes.Length; i++)
        {
            yield return new DataItem(XNodes[i], Values[2 * i], Values[2 * i + 1]);
        }
    }

    public static bool Save(string filename, in V1DataArray data)
    {
        try
        {
            using (StreamWriter fs = new StreamWriter(filename))
            {
                Console.WriteLine("Сохраняем данные...");

                Console.WriteLine(JsonSerializer.Serialize(data.Key));
                fs.WriteLine(JsonSerializer.Serialize(data.Key));
                Console.WriteLine(JsonSerializer.Serialize(data.Date));
                fs.WriteLine(JsonSerializer.Serialize(data.Date));
                Console.WriteLine(JsonSerializer.Serialize(data.XNodes));
                fs.WriteLine(JsonSerializer.Serialize(data.XNodes));
                var y1Values = data.Values.Where((v, index) => index % 2 == 0).ToArray();
                var y2Values = data.Values.Where((v, index) => index % 2 == 1).ToArray();
                Console.WriteLine(JsonSerializer.Serialize(y1Values));
                fs.WriteLine(JsonSerializer.Serialize(y1Values));
                Console.WriteLine(JsonSerializer.Serialize(y2Values));
                fs.WriteLine(JsonSerializer.Serialize(y2Values));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
        return true;
    }

    public static bool Load(string filename, out V1DataArray data)
    {
        data = null;
        try
        {
            using (StreamReader sr = new StreamReader(filename))
            {
                data = new V1DataArray("", DateTime.Now);
                
                string? key = JsonSerializer.Deserialize<string>(sr.ReadLine());
                if (key == null) throw new InvalidDataException("Key is missing or invalid.");
                data.Key = key;

                string? dateStr = JsonSerializer.Deserialize<string>(sr.ReadLine());
                if (dateStr == null) throw new InvalidDataException("Date is missing or invalid.");
                data.Date = DateTime.Parse(dateStr);

                double[]? xNodes = JsonSerializer.Deserialize<double[]>(sr.ReadLine());
                if (xNodes == null) throw new InvalidDataException("XNodes are missing or invalid.");
                data.XNodes = xNodes;

                double[]? y1Values = JsonSerializer.Deserialize<double[]>(sr.ReadLine());
                double[]? y2Values = JsonSerializer.Deserialize<double[]>(sr.ReadLine());
                
                if (y1Values == null || y2Values == null) 
                    throw new InvalidDataException("Y1 or Y2 values are missing or invalid.");

                data.Values = new double[data.XNodes.Length * 2];
                for (int i = 0; i < data.XNodes.Length; i++)
                {
                    data.Values[2 * i] = y1Values[i];
                    data.Values[2 * i + 1] = y2Values[i];
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        return true;
    }
}

public class V1MainCollection : List<V1Data>, IEnumerable<DataItem>
{
    public V1Data this[string key] =>
        this.Find(item => item.Key == key) ?? throw new KeyNotFoundException($"Элемент с ключом '{key}' не найден.");

    public new bool Add(V1Data v1Data)
    {
        if ((this as IEnumerable<V1Data>).Any(item => item.Key == v1Data.Key && item.Date == v1Data.Date))
        {
            return false;
        }
        base.Add(v1Data);
        return true;
    }

    public V1MainCollection(int nA, int nL)
    {
        Random rnd = new Random();

        double[] GenerateXArray(int count) =>
            Enumerable.Range(0, count).Select(i => i * 0.1 + rnd.NextDouble()).ToArray();

        void FGenerator(double x, ref double y1, ref double y2)
        {
            y1 = x;
            y2 = x * 3;
        }

        DataItem DataItemGenerator(double x) => new DataItem(x, x * 2, x * 4);

        for (int i = 0; i < nA; i++) Add(new V1DataArray($"Array_{i}", DateTime.Now, GenerateXArray(5), FGenerator));
        // Add(new V1DataArray($"Array_33", DateTime.Now, GenerateXArray(3), FGenerator));
        for (int i = 0; i < nL; i++) Add(new V1DataList($"List_{i}", DateTime.Now, GenerateXArray(5), DataItemGenerator));
    }

    public override string ToString() => $"V1MainCollection: {Count} elements\n" + string.Join("\n", this);

    public string ToLongString(string format) =>
        $"V1MainCollection (detailed): {Count} elements\n" + string.Join("\n", this.Cast<V1Data>().Select(item => item.ToLongString(format)));

    public new IEnumerator<DataItem> GetEnumerator() => this.Cast<V1Data>().SelectMany(d => d).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public double MaxY1Module => (this as IEnumerable<V1Data>).SelectMany(d => d).Select(di => Math.Abs(di.Y1)).DefaultIfEmpty(-1).Max();

    public IEnumerable<double>? RepeatingXCoordinates =>
        (this as IEnumerable<V1Data>).SelectMany(d => d)
            .GroupBy(di => di.X)
            .Where(g => g.Select(di => di).Select(di =>
                (this as IEnumerable<V1Data>).Count(d => d.Any(x => x.X == di.X)) > 1).FirstOrDefault())
            .Select(g => g.Key)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
}

class Program
{
    static void Debug_FileIO()
    {
        var x = new double[] { 1, 2, 3 };
        
        void F(double x, ref double y1, ref double y2)
        {
            y1 = x;
            y2 = x * 2;
        }

        var array = new V1DataArray("SaveTest", DateTime.Now, x, F);
        
        string file = "data.json";

        Console.WriteLine("Попытка сохранить данные в файл...");
        if (V1DataArray.Save(file, array))
        {
            Console.WriteLine("Данные успешно сохранены.");

            V1DataArray? loaded = null;
            if (V1DataArray.Load(file, out loaded))
            {
                Console.WriteLine("Данные успешно загружены.");
                if (loaded != null)
                {
                    Console.WriteLine(loaded.ToLongString("F2"));
                }
            }
            else
            {
                Console.WriteLine("Ошибка при загрузке данных.");
            }
        }
        else
        {
            Console.WriteLine("Ошибка при сохранении данных.");
        }
    }


    static void Debug_LinqAndEnumeration()
    {
        V1MainCollection col = new(0, 0);

        void F(double x, ref double y1, ref double y2) => (y1, y2) = (x, x + 1);
        DataItem FDI(double x) => new(x, x, x + 1);

        col.Add(new V1DataArray("empty array", DateTime.Now));
        col.Add(new V1DataList("empty list", DateTime.Now));
        col.Add(new V1DataArray("arr1", DateTime.Now, new double[] { 1, 2, 3 }, F));
        col.Add(new V1DataList("list1", DateTime.Now, new double[] { 2, 3, 4 }, FDI));

        Console.WriteLine("\n Коллекция:");
        Console.WriteLine(col.ToLongString("F2"));

        Console.WriteLine("\nВсе элементы DataItem в коллекции:");
        foreach (var item in col)
            Console.WriteLine(item);

        Console.WriteLine("\nМаксимальный модуль Y1 среди всех DataItem: " + col.MaxY1Module);

        Console.WriteLine("\nПовторяющиеся координаты X (встречаются в двух и более элементах):");
        var repeatX = col.RepeatingXCoordinates;
        if (repeatX == null) Console.WriteLine("null");
        else foreach (var xCoord in repeatX) Console.WriteLine(xCoord);
    }

    static void Main()
    {
        Debug_FileIO();
        Debug_LinqAndEnumeration();
    }
}
