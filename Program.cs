using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShtankoEkaterina_193_3
{
    static class Program
    {
        /// <summary>
        /// Словарь, соотносящий таблицу Dim c соответствующей таблицей фактов.
        /// </summary>
        private static readonly Dictionary<string, string> dimFactDictionary = new Dictionary<string, string>
        {
            {"DimProduct", "FactResellerSales.ProductKey"},
            {"DimReseller", "FactResellerSales.ResellerKey"},
            {"DimCurrency", "FactResellerSales.CurrencyKey"},
            {"DimPromotion", "FactResellerSales.PromotionKey"},
            {"DimSalesTerritory", "FactResellerSales.SalesTerritoryKey"},
            {"DimEmployee", "FactResellerSales.EmployeeKey"},
            {"DimDate", "FactResellerSales.OrderDateKey"}
        };

        /// <summary>
        /// Словарь, соотносящий таблицу и список полей в ней.
        /// </summary>
        private static readonly Dictionary<string, List<string>> dimFieldsDictionary = new Dictionary<string, List<string>>
        {
            {
                "FactResellerSales",
                new List<string>{ "ProductKey", "OrderDateKey", "ResellerKey", "EmployeeKey", "PromotionKey",
                    "CurrencyKey", "SalesTerritoryKey", "SalesOrderNumber","SalesOrderLineNumber",
                    "OrderQuantity", "CarrierTrackingNumber","Customer–PONumber" }
            },

            {
                "DimProduct",
                new List<string>{ "ProductKey", "ProductAlternateKey", "EnglishProductName", "Color",
                    "SafetyStockLevel", "ReorderPoint", "SizeRange", "DaysToManufacture", "StartDate" }
            },

            {
                "DimReseller",
                new List<string>{ "ResellerKey", "ResellerAlternateKey", "Phone", "BusinessType",
                    "ResellerName", "NumberEmployees", "OrderFrequency","ProductLine",
                    "AddressLine1", "BankName", "YearOpened" }
            },

            {
                "DimCurrency",
                new List<string>{ "CurrencyKey", "CurrencyAlternateKey", "CurrencyName" }
            },

            {
                "DimPromotion",
                new List<string>{ "PromotionKey", "PromotionAlternateKey", "EnglishPromotionName",
                    "EnglishPromotionType", "EnglishPromotionCategory", "StartDate", "EndDate", "MinQty" }
            },

            {
                "DimSalesTerritory",
                new List<string>{ "SalesTerritoryKey", "SalesTerritoryAlternateKey", "SalesTerritoryRegion",
                    "SalesTerritoryCountry", "SalesTerritoryGroup" }
            },

            {
                "DimEmployee",
                new List<string>{ "EmployeeKey", "FirstName", "LastName", "Title", "BirthDate", "LoginID",
                    "EmailAddress", "Phone", "MaritalStatus", "Gender", "PayFrequency",
                    "VacationHours", "SickLeaveHours", "DepartmentName", "StartDate" }
            },

            {
                "DimDate",
                new List<string> { "DateKey", "FullDateAlternateKey", "DayNumberOfWeek",
                    "EnglishDayNameOfWeek", "DayNumberOfMonth", "DayNumberOfYear",
                    "WeekNumberOfYear", "EnglishMonthName", "MonthNumberOfYear",
                    "CalendarQuarter", "CalendarYear", "CalendarSemester", "FiscalQuarter",
                    "FiscalYear", "FiscalSemester" }
            }
        };

        static void Main(string[] args)
        {
            try
            {
                string[] input = File.ReadAllLines(Path(args[1]));
                string[] outputData = input[0].Split(',');
                int numberOfFilters = int.Parse(input[1]);
                string tableName = "";
                string fieldName = "";
                string op = "";
                string valueToCompareWith = "";
                RoaringBitmap mainRoaringBitmap = new RoaringBitmap();
                for (int i = 0; i < numberOfFilters; i++)
                {
                    int indexOfFieldInTable = 0;
                    string[] filterString = input[2 + i].Split(' ');
                    FilterParser(filterString, ref tableName, ref fieldName, ref indexOfFieldInTable,
                        ref op, ref valueToCompareWith);
                    string[] lines = File.ReadAllLines(Path(args[0]) + "/" + "/" + tableName + ".csv");

                    RoaringBitmap roaringBitmap = FilterRoaring(lines, indexOfFieldInTable, op, valueToCompareWith);

                    if (filterString[0].Contains("Dim"))
                    {
                        roaringBitmap = FromDimToFactRoaring(roaringBitmap, lines, tableName, args);
                    }
                    if (i == 0)
                        mainRoaringBitmap = roaringBitmap;
                    else
                        mainRoaringBitmap?.And(roaringBitmap);
                }
                bool empty = numberOfFilters == 0 ? true : false;

                Output(mainRoaringBitmap, outputData, empty, args);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        private static RoaringBitmap FilterRoaring(string[] lines, int indexOfFieldInTable, string op, string valueToCompareWith)
        {
            RoaringBitmap roaringBitmap = new RoaringBitmap();
            // Проверяем каждую строку таблицы на соответствие поступившему условию
            // фильтрации и устанавливаем соответствующее значение в roaringBitmap.
            for (int j = 0; j < lines.Length; j++)
            {
                string field = lines[j].Split('|')[indexOfFieldInTable];
                bool res = Check(field, op, valueToCompareWith);
                roaringBitmap.Set(j, res);
            }
            return roaringBitmap;
        }

        private static RoaringBitmap FromDimToFactRoaring(RoaringBitmap roaringBitmap, string[] linesDim, string tableName, string[] args)
        {
            // Содержит "ключи" строк таблицы, прошедших фильтрацию.
            List<int> keysAfterFiltering = new List<int>();
            // Пробегаем по всем строкам Dim-таблицы.
            for (int j = 0; j < linesDim.Length; j++)
            {
                // (Если строка прошла фильтрацию) == (Get выдаст true) -> добавляем "ключ" в лист.
                if (roaringBitmap.Get(j))
                {
                    if (tableName == "DimDate")
                    {
                        keysAfterFiltering.Add(int.Parse(linesDim[j].Split('|')[0]));
                    }
                    else
                        // Добавляем "1", так как нумерация строк в таблице начинается с 1,
                        // а массивы - с 0.
                        keysAfterFiltering.Add(j + 1);
                }
            }
            // Таблица фактов, соответствующая данной таблице Dim.
            string factTable = dimFactDictionary[tableName];
            string[] linesInFactTable = File.ReadAllLines(Path(args[0]) + "/" + factTable + ".csv");
            RoaringBitmap new_roaringBitmap = new RoaringBitmap();
            for (ushort j = 0; j < linesInFactTable.Length; j++)
            {
                // Если ключ, содержащийся в строке таблицы фактов, соответствует какому-либо
                // ключу в keysAfterFiltering добавляем его в roaringBitmap.
                if (keysAfterFiltering.Any(indexOfFieldInTablees => linesInFactTable[j] == indexOfFieldInTablees.ToString()))
                {
                    new_roaringBitmap.Set(j, true);
                }
            }
            return new_roaringBitmap;
        }

        private static void FilterParser(string[] filterString, ref string tableName, ref string fieldName,
            ref int indexOfFieldInTable, ref string op, ref string valueToCompareWith)
        {
            //Например, "DimPromotion.MinQty <> 0".
            if (filterString[0].Contains("Dim"))
            {
                // "DimPromotion"
                tableName = filterString[0].Split('.')[0];
                // "MintQty"
                fieldName = filterString[0].Split('.')[1];
                indexOfFieldInTable = dimFieldsDictionary[tableName].IndexOf(fieldName);
            }
            else
            {
                // Например, "FactResellerSales.OrderQuantity".
                tableName = filterString[0];
            }
            op = filterString[1];
            valueToCompareWith = String.Join(" ", filterString.Skip(2)).Replace("\'", "");
        }

        private static void Output(RoaringBitmap mainRoaringBitmap, string[] outputData, bool empty, string[] args)
        {
            List<string> outputLines = new List<string>(mainRoaringBitmap.GetSize());
            int indexInOutputLines = 0;
            bool thisIsTheFirstTable = true;
            foreach (var table in outputData)
            {
                // Проверяя есть ли какая-то информацая уже в outputLines, мы проверяем
                // первая ли таблица из outputData нами обработана. Это нужно для корректного
                // формата вывода.
                if (outputLines.Count > 0)
                {
                    thisIsTheFirstTable = false;
                }
                if (table.Contains("Fact"))
                {
                    string[] lines = File.ReadAllLines(Path(args[0]) + "/" + table + ".csv");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (empty == true ? true : mainRoaringBitmap.Get(i))
                        {
                            if (!thisIsTheFirstTable)
                            {
                                outputLines[indexInOutputLines] = outputLines[indexInOutputLines] + '|' + lines[i];
                                indexInOutputLines++;
                            }
                            else
                                outputLines.Add(lines[i]);
                        }
                    }
                    indexInOutputLines = 0;
                }
                else
                {
                    string tableName = table.Split('.')[0];
                    string fieldName = table.Split('.')[1];
                    string[] Dimlines = File.ReadAllLines(Path(args[0]) + "/" + tableName + ".csv");
                    int indexOfFieldInTable = dimFieldsDictionary[tableName].IndexOf(fieldName);
                    string factTable = dimFactDictionary[tableName];
                    string[] factLines = File.ReadAllLines(Path(args[0]) + "/" + factTable + ".csv");
                    List<string> keys = new List<string>();
                    for (int i = 0; i < factLines.Length; i++)
                    {
                        bool keyAdded = false;
                        if (empty == true ? true : mainRoaringBitmap.Get(i))
                        {
                            keys.Add(factLines[i]);
                            keyAdded = true;
                        }
                        if (keyAdded)
                        {
                            for (int j = 0; j < Dimlines.Length; j++)
                            {
                                if (keys.Last() == Dimlines[j].Split('|')[0])
                                {
                                    var field = Dimlines[j].Split('|')[indexOfFieldInTable];
                                    if (!thisIsTheFirstTable)
                                    {
                                        outputLines[indexInOutputLines] = outputLines[indexInOutputLines] + '|' + field;
                                        indexInOutputLines++;
                                    }
                                    else
                                        outputLines.Add(field);
                                    break;
                                }
                            }
                        }
                    }
                    indexInOutputLines = 0;
                }
            }

            WriteToFile(outputLines, args);
        }

        /// <summary>
        /// Выводин информацию в файл.
        /// </summary>
        /// <param name="outputLines"></param>
        /// <param name="args"></param>
        private static void WriteToFile(List<string> outputLines, string[] args)
        {
            string path = Path(args[2]);
            if (File.Exists(path)) { File.WriteAllText(path, ""); }
            foreach (var line in outputLines)
            {
                File.AppendAllText(path, line + "\n");
            }
        }

        /// <summary>
        /// Производит проверку по условию фильтрации.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="op"></param>
        /// <param name="valueToCompareWith"></param>
        /// <returns></returns>
        private static bool Check(string value, string op, string valueToCompareWith)
        {
            switch (op)
            {
                case "=":
                    {
                        return value == valueToCompareWith;
                    }
                case "<>":
                    {
                        return value != valueToCompareWith;
                    }
                case "<":
                    {
                        return int.Parse(value) < int.Parse(valueToCompareWith);
                    }
                case "<=":
                    {
                        return int.Parse(value) <= int.Parse(valueToCompareWith);
                    }
                case ">":
                    {
                        return int.Parse(value) > int.Parse(valueToCompareWith);
                    }
                case ">=":
                    {
                        return int.Parse(value) >= int.Parse(valueToCompareWith);
                    }
                default:
                    {
                        throw new ArgumentException("Неверные данные. Неизвестный оператор.");
                    }
            }
        }

        /// <summary>
        /// Данный метод позволит работать с файлами как через полный, так и через относительный пути.
        /// </summary>
        /// <param name="path">Введенный через командную строку путь.</param>
        /// <returns></returns>
        static string Path(string path)
        {
            if (File.Exists(path))
                return path;
            if (Directory.Exists(path))
                return path;
            if (File.Exists("input/" + path))
                return "input/" + path;
            if (File.Exists("output/" + path))
                return "output/" + path;
            if (File.Exists("/" + path))
                return "/" + path;
            throw new FileNotFoundException("Ошибка. Такого файла не существует.");
        }
    }

    public abstract class Container
    {
        public abstract int Size();
    }

    public class BitmapContainer : Container
    {
        private int size;

        public ushort[] container { get; set; } = new ushort[(int)Math.Pow(2, 16)];

        public void Add(int value)
        {
            if (!Exist(value))
            {
                container[value / 16] |= (ushort)(1 << value % 16);
                size++;
            }
        }

        public void Del(int value)
        {
            if (Exist(value))
            {
                container[value / 16] &= (ushort)~(1 << value % 16);
                size--;
            }
        }

        public override int Size()
        {
            // Альтернативное решение: 
            // Мы могли бы использовать BitCount, но решение с использованием size
            // показалось мне более эфективным. Так программа работает быстрее.

            //int count = 0;
            //foreach (var item in container)
            //{
            //    count += BitCount(item);
            //}
            //return count;

            return size;
        }

        //int BitCount(uint num)
        //{
        //    int count = 0;
        //    while (num > 0)
        //    {
        //        count += (int)num & 1;
        //        num = num >> 1;
        //    }
        //    return count;
        //}

        public bool Exist(int value)
        {
            return (container[value / 16] >> (value % 16)) % 2 == 1;
        }
    }

    public abstract class Bitmap
    {
        public abstract void And(Bitmap other);

        public abstract void Set(int i, bool value);

        public abstract bool Get(int i);
    }

    public class ArrayContainer : Container
    {
        ushort[] container = new ushort[0];

        public ushort[] Container
        {
            get
            {
                return container;
            }
            set
            {
                container = value;
            }
        }

        public void Add(ushort value)
        {
            if (!Exist(value))
            {
                Array.Resize(ref container, container.Length + 1);
                container[container.Length - 1] = value;
                Array.Sort(container);
            }
        }

        public void Del(ushort value)
        {
            if (Exist(value))
            {
                int index = Array.IndexOf(container, value);
                for (int a = index; a < container.Length - 1; a++)
                {
                    container[a] = container[a + 1];
                }
                Array.Resize(ref container, container.Length - 1);
            }
        }

        public override int Size()
        {
            return Container.Length;
        }

        public bool Exist(ushort value)
        {
            // Можно было бы использовать Array.Exists.
            return Array.BinarySearch(container, value) >= 0;
        }
    }

    public class RoaringBitmap : Bitmap
    {
        /// <summary>
        /// Массив контенеров данного RoaringBitmap("Array of containers")
        /// </summary>
        Container[] containers = new Container[0];

        public override void Set(int i, bool value)
        {
            // Старшие биты поступившего эл-та, верхние 16 бит, старшее слово
            ushort mostSignificantBit = (ushort)(i >> 16);

            // Проверяем, существует ли уже контенер с ключем поступившего элемента(индекса i)
            // Если такой контенер еще не был создан - создаем новый ArrayContainer 
            if (!(containers.Length > mostSignificantBit && containers[mostSignificantBit] != null))
            {
                Array.Resize(ref containers, mostSignificantBit + 1);
                containers[mostSignificantBit] = new ArrayContainer();
            }

            if (containers[mostSignificantBit] as BitmapContainer != null)
            {
                if (value)
                {
                    (containers[mostSignificantBit] as BitmapContainer).Add(i);
                }
                else
                {
                    (containers[mostSignificantBit] as BitmapContainer).Del(i);
                    //Если кол-во единиц в BitmapContainer меньше или равно 4096 - меняем его на ArrayContainer
                    if ((containers[mostSignificantBit] as BitmapContainer).Size() <= 4096)
                    {
                        ArrayContainer new_arrayContainer = new ArrayContainer();
                        //(int)Math.Pow(2, 16) - кол-во элементов в битмап контенере. 
                        for (int j = 0; j < (int)Math.Pow(2, 16); j++)
                        {
                            if ((containers[mostSignificantBit] as BitmapContainer).Exist(j))
                            {
                                // Мы добавляем в эррей контенер значение % (int)Math.Pow(2, 16), так
                                // как у всех элементов в контейнере общее старшее слово, общие старшие 16 бит,
                                // они - ключ контенера, и их нет смысла хранить для каждого элемента.
                                // То есть, элементы контенера мы кодируем "нижними" 16 битами, младшим словом, а
                                // "верхнии" 16 бит - оставляем на ключ. 
                                new_arrayContainer.Add((ushort)(j % (int)Math.Pow(2, 16)));
                            }
                        }
                        containers[mostSignificantBit] = new_arrayContainer;
                    }
                }
            }
            else if (containers[mostSignificantBit] as ArrayContainer != null)
            {
                if (value)
                {
                    // Мы добавляем в эррей контенер значение % (int)Math.Pow(2, 16), так
                    // как у всех элементов в контейнере общее старшее слово, общие старшие 16 бит,
                    // они - ключ контенера, и их нет смысла хранить для каждого элемента.
                    // То есть, элементы контенера мы кодируем "нижними" 16 битами, младшим словом, а
                    // "верхнии" 16 бит - оставляем на ключ. 
                    (containers[mostSignificantBit] as ArrayContainer).Add((ushort)(i % (int)Math.Pow(2, 16)));
                    //Если элементов в ArrayContainer стало больше чем 4096 - меняем его BitmapContainer 
                    if ((containers[mostSignificantBit] as ArrayContainer).Size() > 4096)
                    {
                        BitmapContainer new_bitmapContainer = new BitmapContainer();
                        foreach (var j in (containers[mostSignificantBit] as ArrayContainer).Container)
                        {
                            if (value)
                                new_bitmapContainer.Add(j);
                        }
                        containers[mostSignificantBit] = new_bitmapContainer;
                    }
                }
                else
                {
                    (containers[mostSignificantBit] as ArrayContainer).Del((ushort)(i % (int)Math.Pow(2, 16)));
                    // Если последний контейнер - пуст, мы удоляем его и все до него, равные null
                    if (containers[mostSignificantBit] == containers[containers.Length - 1] && (containers[mostSignificantBit].Size() == 0))
                    {
                        int num = 1;
                        while (containers[mostSignificantBit] == null || containers[mostSignificantBit].Size() == 0)
                        {
                            while (mostSignificantBit != 0)
                            {
                                mostSignificantBit--;
                            }
                            if (mostSignificantBit == 0)
                            {
                                break;
                            }
                            num++;
                        }
                        Array.Resize(ref containers, containers.Length - num);
                    }
                }
            }
        }

        /// <summary>
        /// Возращает кол-во элементов в RoaringBitmap.
        /// </summary>
        /// <returns></returns>
        public int GetSize()
        {
            int size = 0;
            foreach (var item in containers)
            {
                if (item != null)
                    size += item.Size();
            }
            return size;
        }

        public override bool Get(int i)
        {
            if (containers.Length > i >> 16 && containers[i >> 16] != null)
            {
                if (containers[i >> 16] as BitmapContainer != null)
                {
                    return (containers[i >> 16] as BitmapContainer).Exist(i);

                }
                else if (containers[i >> 16] as ArrayContainer != null)
                {
                    return (containers[i >> 16] as ArrayContainer).Exist((ushort)(i % (int)Math.Pow(2, 16)));
                }
            }
            return false;
        }

        public override void And(Bitmap other)
        {
            int num = Math.Min(containers.Length, (other as RoaringBitmap).containers.Length);
            for (ushort i = 0; i < num; i++)
            {
                if (containers[i] != null && (other as RoaringBitmap).containers[i] != null)
                {
                    // Если оба контенера - битмап контенеры
                    if (containers[i] as BitmapContainer != null
                        && (other as RoaringBitmap).containers[i] as BitmapContainer != null)
                    {
                        //(int)Math.Pow(2, 16) - кол-во элементов в битмап контенере. 
                        for (int j = 0; j < (int)Math.Pow(2, 16); j++)
                            (containers[i] as BitmapContainer).container[j] &=
                                ((other as RoaringBitmap).containers[i] as BitmapContainer).container[j];
                        // После And кол-во единиц в контенере могло уменьшиться.
                        // Если кол-во единиц в BitmapContainer меньше или равно 4096 - меняем его на ArrayContainer
                        if ((containers[i] as BitmapContainer).Size() <= 4096)
                        {
                            ArrayContainer new_arrayContainer = new ArrayContainer();
                            //(int)Math.Pow(2, 16) - кол-во элементов в битмап контенере. 
                            for (int j = 0; j < (int)Math.Pow(2, 16); j++)
                            {
                                if ((containers[i] as BitmapContainer).Exist(j))
                                {
                                    new_arrayContainer.Add((ushort)(j % (int)Math.Pow(2, 16)));
                                }
                            }
                            containers[i] = new_arrayContainer;
                        }
                    }
                    // Если оба контенера - эррей контенеры
                    else if (containers[i] as ArrayContainer != null
                        && (other as RoaringBitmap).containers[i] as ArrayContainer != null)
                    {
                        ArrayContainer new_arrayContainer = new ArrayContainer();
                        foreach (var item in (containers[i] as ArrayContainer).Container)
                        {
                            if (((other as RoaringBitmap).containers[i] as ArrayContainer).Exist(item))
                            {
                                new_arrayContainer.Add(item);
                            }
                        }
                        containers[i] = new_arrayContainer;
                    }
                    // Если контенеры разных видов
                    else
                    {
                        ArrayContainer arrayContainer;
                        BitmapContainer bitmapContainer;

                        if (containers[i] is ArrayContainer)
                        {
                            arrayContainer = containers[i] as ArrayContainer;
                            bitmapContainer = (other as RoaringBitmap).containers[i] as BitmapContainer;
                        }
                        else
                        {
                            arrayContainer = (other as RoaringBitmap).containers[i] as ArrayContainer;
                            bitmapContainer = containers[i] as BitmapContainer;
                        }

                        if (arrayContainer == null || bitmapContainer == null)
                            throw new ArgumentException("Incorrect containers.");
                        ArrayContainer new_arrayContainer = new ArrayContainer();
                        foreach (var item in arrayContainer.Container)
                        {
                            if (bitmapContainer.Exist(item))
                            {
                                new_arrayContainer.Add(item);
                            }
                        }
                        containers[i] = new_arrayContainer;
                    }
                }
                // Если последний контейнер - пуст, мы удоляем его и все до него, равные null
                if (containers[i] == containers[containers.Length - 1] && (containers[i].Size() == 0))
                {
                    num = 1;
                    while (containers[i] == null || containers[i].Size() == 0)
                    {
                        while (i != 0)
                        {
                            i--;
                        }
                        if (i == 0)
                        {
                            break;
                        }
                        num++;
                    }
                    Array.Resize(ref containers, containers.Length - num);
                    break;
                }
            }
        }
    }
}
