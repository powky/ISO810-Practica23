using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Xml.Linq;
using System.Xml;

public class AsientoDB
{
    [BsonElement("_id")]
    public ObjectId Id { get; set; }

    [BsonElement("siting")]
    public int? NumeroAsiento { get; set; }

    [BsonElement("desc")]
    public string? DescripcionAsiento { get; set; }

    [BsonElement("date")]
    public DateTime FechaAsiento { get; set; }

    [BsonElement("code")]
    public string? Codigo { get; set; }

    [BsonElement("name")]
    public string? Nombre { get; set; }

    [BsonElement("acc")]
    public string? Cuenta { get; set; }

    [BsonElement("movement")]
    public string? TipoMovimiento { get; set; }

    [BsonElement("amount")]
    public decimal Monto { get; set; }
}

public class Program
{
    private MongoClient _client;
    private IMongoDatabase _database;
    private IMongoCollection<AsientoDB> _asientosCollection;
    private IMongoCollection<AsientoDB> _asientosInputCollection;

    public Program()
    {
        _client = new MongoClient("mongodb://localhost:27017");
        _database = _client.GetDatabase("unapec");
        _asientosCollection = _database.GetCollection<AsientoDB>("asientos");
        _asientosInputCollection = _database.GetCollection<AsientoDB>("asientos_input");
    }

    public async Task GenerateReport()
    {
        var asientos = await _asientosCollection.Find(new BsonDocument()).ToListAsync();

        if (asientos.Count == 0)
        {
            Console.WriteLine("No hay asientos para generar el reporte.");
            return;
        }

        using (XmlWriter writer = XmlWriter.Create("asientos.xml"))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("AsientoActivos");

            if (asientos.Count > 0)
            {
                var firstAsiento = asientos[0];
                writer.WriteStartElement("Encabezado");
                if (firstAsiento.NumeroAsiento.HasValue)
                {
                    writer.WriteElementString("NumeroAsiento", firstAsiento.NumeroAsiento.Value.ToString());
                }
                if (!string.IsNullOrEmpty(firstAsiento.DescripcionAsiento))
                {
                    writer.WriteElementString("DescripcionAsiento", firstAsiento.DescripcionAsiento);
                }
                writer.WriteElementString("FechaAsiento", firstAsiento.FechaAsiento.ToString("yyyy-MM-dd"));
                writer.WriteEndElement();
            }

            foreach (var asiento in asientos)
            {
                writer.WriteStartElement("Cuentas");
                if (!string.IsNullOrEmpty(asiento.Codigo))
                {
                    writer.WriteElementString("Código", asiento.Codigo);
                }
                if (!string.IsNullOrEmpty(asiento.Nombre))
                {
                    writer.WriteElementString("Nombre", asiento.Nombre);
                }
                if (!string.IsNullOrEmpty(asiento.Cuenta))
                {
                    writer.WriteElementString("Cuenta", asiento.Cuenta);
                }
                if (!string.IsNullOrEmpty(asiento.TipoMovimiento))
                {
                    writer.WriteElementString("TipoMovimiento", asiento.TipoMovimiento);
                }
                if (asiento.Monto != 0)
                {
                    writer.WriteElementString("Monto", asiento.Monto.ToString("F2"));
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        Console.WriteLine("Archivo XML generado de forma satisfactoria.");
    }

    public async Task ProcessInputFile()
    {
        string xmlFilePath = "asientos.xml";
        XDocument xmlDoc = XDocument.Load(xmlFilePath);

        int? numeroAsiento = int.Parse(xmlDoc.Root.Element("Encabezado")?.Element("NumeroAsiento")?.Value ?? throw new InvalidOperationException("Missing 'NumeroAsiento' element."));
        var descripcionAsiento = xmlDoc.Root.Element("Encabezado")?.Element("DescripcionAsiento")?.Value ?? throw new InvalidOperationException("Missing 'DescripcionAsiento' element.");
        var fechaAsiento = DateTime.Parse(xmlDoc.Root.Element("Encabezado")?.Element("FechaAsiento")?.Value ?? throw new InvalidOperationException("Missing 'FechaAsiento' element."));
        var cuentas = xmlDoc.Root.Elements("Cuentas");

        foreach (var cuenta in cuentas)
        {
            var codigo = cuenta.Element("Código")?.Value ?? throw new InvalidOperationException("Missing 'Código' element.");
            var nombre = cuenta.Element("Nombre")?.Value ?? throw new InvalidOperationException("Missing 'Nombre' element.");
            var cuentaAcc = cuenta.Element("Cuenta")?.Value ?? throw new InvalidOperationException("Missing 'Cuenta' element.");
            var tipoMovimiento = cuenta.Element("TipoMovimiento")?.Value ?? throw new InvalidOperationException("Missing 'TipoMovimiento' element.");
            var monto = decimal.Parse(cuenta.Element("Monto")?.Value ?? throw new InvalidOperationException("Missing 'Monto' element."));

            AsientoDB newAsiento = new AsientoDB()
            {
                NumeroAsiento = numeroAsiento,
                DescripcionAsiento = descripcionAsiento,
                FechaAsiento = fechaAsiento,
                Codigo = codigo,
                Nombre = nombre,
                Cuenta = cuentaAcc,
                TipoMovimiento = tipoMovimiento,
                Monto = monto
            };

            await _asientosInputCollection.InsertOneAsync(newAsiento);
        }

        Console.WriteLine("Datos insertados en la colección 'asientos_input' con éxito.");
    }

    public async Task ShowMenu()
    {
        while (true)
        {
            Console.WriteLine("\nEscoge una opción:");
            Console.WriteLine("1. Exportar archivo de nómina.");
            Console.WriteLine("2. Importar archivo de nómina.");
            Console.WriteLine("3. Salir");

            string userChoice = Console.ReadLine();

            switch (userChoice)
            {
                case "1":
                    await GenerateReport();
                    break;
                case "2":
                    await ProcessInputFile();
                    break;
                case "3":
                    Console.WriteLine("Saliendo del programa.");
                    return;
                default:
                    Console.WriteLine("Opción inválida, por favor digita una opción del menú.");
                    break;
            }
        }
    }

    public static async Task Main(string[] args)
    {
        Program program = new Program();
        await program.ShowMenu();
    }
}