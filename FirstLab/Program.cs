using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public delegate void FValues(double x, ref Complex y1, ref Complex y2);
public delegate DataItem FDI(double x);

public struct DataItem
{
    public double X { get; set; }
    public Complex Y1 { get; set; }
    public Complex Y2 { get; set; }

    public DataItem(double x, Complex y1, Complex y2)
    {
        X = x;
        Y1 = y1;
        Y2 = y2;
    }

    public override string ToString() => $"X: {X}, Y1: {Y1}, Y2: {Y2}";

    public string ToString(string format) =>
        $"X: {X.ToString(format)}, Y1: {Y1.ToString(format)}, Y2: {Y2.ToString(format)}";
}

public abstract class V1Data
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
                double diff = (item.Y1 - item.Y2).Magnitude;
                if (diff < min) min = diff;
                if (diff > max) max = diff;
            }
            return (min, max);
        }
    }

    public static explicit operator V1DataArray(V1DataList source)
    {
        double[] xArray = source.Data.Select(item => item.X).ToArray();
        Complex[] valuesArray = new Complex[source.Data.Count * 2];

        for (int i = 0; i < source.Data.Count; i++)
        {
            valuesArray[2 * i] = source.Data[i].Y1;
            valuesArray[2 * i + 1] = source.Data[i].Y2;
        }

        return new V1DataArray(source.Key, source.Date, xArray, 
            (double x, ref Complex y1, ref Complex y2) =>
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
}

public class V1DataArray : V1Data
{
    public double[] XNodes { get; set; } = Array.Empty<double>();
    public Complex[] Values { get; set; } = Array.Empty<Complex>();

    public V1DataArray(string key, DateTime date) : base(key, date) { }

    public V1DataArray(string key, DateTime date, double[] x, FValues F) : base(key, date)
    {
        XNodes = x;
        Values = new Complex[x.Length * 2];

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
                double diff = Complex.Abs(Values[2 * i] - Values[2 * i + 1]);
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
}

public class V1MainCollection : List<V1Data>
{
    public V1Data this[string key] =>
        this.Find(item => item.Key == key) ?? throw new KeyNotFoundException($"Элемент с ключом '{key}' не найден.");

    public new bool Add(V1Data v1Data)
    {
        if (this.Any(item => item.Key == v1Data.Key && item.Date == v1Data.Date))
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

        void FGenerator(double x, ref Complex y1, ref Complex y2)
        {
            y1 = new Complex(x, 0);
            y2 = new Complex(x * 3, 0);
        }

        DataItem DataItemGenerator(double x) => new DataItem(x, new Complex(x, x * 2), new Complex(x * 3, x * 4));

        for (int i = 0; i < nA; i++) Add(new V1DataArray($"Array_{i}", DateTime.Now, GenerateXArray(5), FGenerator));
        Add(new V1DataArray($"Array_33", DateTime.Now, GenerateXArray(3), FGenerator));
        for (int i = 0; i < nL; i++) Add(new V1DataList($"List_{i}", DateTime.Now, GenerateXArray(5), DataItemGenerator));
    }

    public override string ToString() => $"V1MainCollection: {Count} elements\n" + string.Join("\n", this);

    public string ToLongString(string format) =>
        $"V1MainCollection (detailed): {Count} elements\n" + string.Join("\n", this.Select(item => item.ToLongString(format)));
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== 1. V1DataList и его преобразование ===");

        double[] xList = { 0.1, 0.2, 0.3, 0.4, 0.5 };
        // DataItem FDI_Generator(double x) => new DataItem(x, new Complex(x, x * 2), new Complex(x * 3, x * 4));
        DataItem FDI_Generator(double x) => new DataItem(x, new Complex(x, 0), new Complex(x * 3, 0));

        V1DataList dataList = new("List_1", DateTime.Now, xList, FDI_Generator);
        Console.WriteLine(dataList.ToLongString("F2"));

        V1DataArray dataArrayFromList = (V1DataArray)dataList;
        Console.WriteLine("Преобразованный V1DataArray:");
        Console.WriteLine(dataArrayFromList.ToLongString("F2"));


        Console.WriteLine("\n=== 2. Индексатор в V1DataArray ===");

        double[] xArray = { 1.0, 2.0, 3.0, 4.0 };
        void FValues_Generator(double x, ref Complex y1, ref Complex y2)
        {
            y1 = new Complex(x, x * 2);
            y2 = new Complex(x * 3, x * 4);
        }

        V1DataArray dataArray = new("Array_1", DateTime.Now, xArray, FValues_Generator);
        Console.WriteLine($"Элемент с индексом 1: {dataArray[1]}");
        Console.WriteLine($"Элемент с индексом 10 (выход за границы): {dataArray[10]}");


        Console.WriteLine("\n=== 3. Создать и вывести V1MainCollection ===");

        V1MainCollection collection = new(2, 2);
        Console.WriteLine(collection.ToLongString("F2"));


        Console.WriteLine("\n=== 4. Свойства xLength и MinMaxDifference ===");

        foreach (var data in collection)
        {
            Console.WriteLine($"{data}: xLength = {data.XLength}, MinMaxDifference = {data.MinMaxDifference}");
        }


        Console.WriteLine("\n=== 5. Поиск в коллекции по Key ===");

        string existingKey = collection[0].Key;
        string nonExistingKey = "NotExists";

        Console.WriteLine($"Элемент с ключом {existingKey}: {collection[existingKey]}");
        
        try
        {
            Console.WriteLine($"Элемент с ключом {nonExistingKey}: {collection[nonExistingKey]}");
        }
        catch (KeyNotFoundException e)
        {
            Console.WriteLine($"Ошибка: {e.Message}");
        }
    }
}

