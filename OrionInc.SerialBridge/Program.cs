
using System.Globalization;
using System.IO.Ports;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// ============================================================
// CONFIGURAÇÃO
// ============================================================

string API_BASE_URL =
    Environment.GetEnvironmentVariable("ORION_API_URL")
    ?? "http://localhost:52500";

string SERIAL_PORT =
    Environment.GetEnvironmentVariable("ORION_SERIAL_PORT")
    ?? "COM9";

int BAUD_RATE =
    int.TryParse(
        Environment.GetEnvironmentVariable("ORION_BAUD_RATE"),
        out var baud)
        ? baud
        : 9600;

int SENSOR_ID =
    int.TryParse(
        Environment.GetEnvironmentVariable("ORION_SENSOR_ID"),
        out var sensorId)
        ? sensorId
        : 1;

// Nunca mantenha o token do dispositivo no código-fonte.
// Configure ORION_DEVICE_TOKEN no ambiente da máquina.
string DEVICE_TOKEN =
    Environment.GetEnvironmentVariable("ORION_DEVICE_TOKEN")
    ?? string.Empty;

// ============================================================
// HTTP
// ============================================================

using var http = new HttpClient
{
    BaseAddress = new Uri(API_BASE_URL)
};

http.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json"));

http.DefaultRequestHeaders.Add(
    "X-Device-Token",
    DEVICE_TOKEN);

// ============================================================
// SERIAL
// ============================================================

using var serial = new SerialPort(SERIAL_PORT, BAUD_RATE)
{
    ReadTimeout = 1500,
    NewLine = "\n",
    Encoding = Encoding.UTF8
};

// ============================================================
// INÍCIO
// ============================================================

Console.WriteLine("==============================================");
Console.WriteLine("       ORION INC - SERIAL BRIDGE");
Console.WriteLine("==============================================");
Console.WriteLine();

Console.WriteLine($"API:    {API_BASE_URL}");
Console.WriteLine($"PORTA:  {SERIAL_PORT}");
Console.WriteLine($"BAUD:   {BAUD_RATE}");
Console.WriteLine($"SENSOR: {SENSOR_ID}");
Console.WriteLine();

if (DEVICE_TOKEN == "COLOQUE_SEU_NOVO_TOKEN_AQUI" ||
    string.IsNullOrWhiteSpace(DEVICE_TOKEN))
{
    Console.WriteLine("ERRO: Configure o Device Token.");
    Console.WriteLine("Pressione ENTER para sair...");
    Console.ReadLine();
    return;
}

// ============================================================
// TESTAR API
// ============================================================

Console.WriteLine("Testando API...");

try
{
    using var response = await http.GetAsync("/api/v1/health");

    var body = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"API HTTP {(int)response.StatusCode}");
    Console.WriteLine(body);
    Console.WriteLine();

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine("A API não respondeu corretamente.");
        Console.ReadLine();
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine("ERRO AO CONECTAR NA API:");
    Console.WriteLine(ex.Message);
    Console.ReadLine();
    return;
}

// ============================================================
// ABRIR COM9
// ============================================================

try
{
    serial.Open();

    Console.WriteLine("==============================================");
    Console.WriteLine("ARDUINO CONECTADO");
    Console.WriteLine($"Porta: {SERIAL_PORT}");
    Console.WriteLine("Aguardando dados...");
    Console.WriteLine("==============================================");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine("ERRO AO ABRIR A PORTA SERIAL:");
    Console.WriteLine(ex.Message);
    Console.WriteLine();
    Console.WriteLine("Verifique:");
    Console.WriteLine("- Arduino conectado");
    Console.WriteLine("- Porta COM9 correta");
    Console.WriteLine("- Monitor Serial do Arduino fechado");
    Console.ReadLine();
    return;
}

// ============================================================
// HEARTBEAT
// ============================================================

var heartbeatTask = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            await EnviarHeartbeatAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HEARTBEAT] ERRO: {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(15));
    }
});

// ============================================================
// LEITURA SERIAL
// ============================================================

while (true)
{
    try
    {
        string linha = serial.ReadLine().Trim();

        if (string.IsNullOrWhiteSpace(linha))
            continue;

        Console.WriteLine($"ARDUINO > {linha}");

        if (linha.StartsWith("ORION_DATA;"))
        {
            await ProcessarDadosAsync(linha);
        }
        else if (linha.StartsWith("ORION_EVENTO;"))
        {
            await ProcessarEventoAsync(linha);
        }
    }
    catch (TimeoutException)
    {
        // Timeout normal da porta serial.
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SERIAL] ERRO: {ex.Message}");
    }
}

// ============================================================
// PROCESSAR DADOS
// ============================================================

async Task ProcessarDadosAsync(string linha)
{
    try
    {
        var partes = linha.Split(';');

        double? distancia = null;
        double? peso = null;

        foreach (var parte in partes)
        {
            if (parte.StartsWith("DISTANCIA="))
            {
                string valor = parte["DISTANCIA=".Length..];

                if (double.TryParse(
                    valor,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double resultadoDistancia)
                    && resultadoDistancia >= 0)
                {
                    distancia = resultadoDistancia;
                }
                else
                {
                    Console.WriteLine(
                        "  [DISTANCIA] Leitura inválida. Não será enviada.");
                }
            }
            else if (parte.StartsWith("PESO="))
            {
                string valor = parte["PESO=".Length..];

                if (double.TryParse(
                    valor,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double resultadoPeso))
                {
                    peso = resultadoPeso;
                }
                else
                {
                    Console.WriteLine(
                        "  [PESO] Leitura inválida. Não será enviada.");
                }
            }
        }

        // ----------------------------------------------------
        // DISTÂNCIA
        // ----------------------------------------------------

        if (distancia.HasValue)
        {
            Console.WriteLine(
                $"  Distância: {distancia.Value:F2} cm");

            await EnviarLeituraAsync(
                "distancia",
                distancia.Value,
                "cm");
        }

        // ----------------------------------------------------
        // PESO
        // ----------------------------------------------------

        if (peso.HasValue)
        {
            Console.WriteLine(
                $"  Peso RAW: {peso.Value:F0}");

            await EnviarLeituraAsync(
                "peso",
                peso.Value,
                "g");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DADOS] ERRO: {ex.Message}");
    }
}

// ============================================================
// PROCESSAR EVENTOS
// ============================================================

async Task ProcessarEventoAsync(string linha)
{
    try
    {
        var partes = linha.Split(';');

        string tipo = "";
        string motivo = "Evento recebido do Arduino";

        foreach (var parte in partes)
        {
            if (parte.StartsWith("TIPO="))
            {
                tipo = parte["TIPO=".Length..];
            }
            else if (parte.StartsWith("MOTIVO="))
            {
                motivo = parte["MOTIVO=".Length..];
            }
        }

        if (string.IsNullOrWhiteSpace(tipo))
        {
            Console.WriteLine("  [EVENTO] Tipo não informado.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine($"EVENTO DETECTADO: {tipo}");
        Console.WriteLine($"MOTIVO: {motivo}");
        Console.WriteLine("==============================================");

        string nivel = tipo switch
        {
            "FALHA_SENSOR" => "alto",
            "ALERTA" => "alto",
            "RECUPERACAO_SENSOR" => "informativo",
            _ => "medio"
        };

        var payload = new
        {
            type = tipo,
            description = motivo,
            level = nivel
        };

        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await http.PostAsync(
            $"/api/v1/sensors/{SENSOR_ID}/events",
            content);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"  API EVENTO: {(int)response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("  Evento enviado com sucesso.");
        }
        else
        {
            Console.WriteLine("  ERRO AO ENVIAR EVENTO:");
            Console.WriteLine(body);
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[EVENTO] ERRO: {ex.Message}");
    }
}

// ============================================================
// ENVIAR LEITURA
// ============================================================

async Task EnviarLeituraAsync(
    string metrica,
    double valor,
    string unidade)
{
    var payload = new
    {
        metric = metrica,
        value = valor,
        unit = unidade,
        deviceTimestamp = DateTimeOffset.UtcNow
    };

    var json = JsonSerializer.Serialize(payload);

    using var content = new StringContent(
        json,
        Encoding.UTF8,
        "application/json");

    try
    {
        using var response = await http.PostAsync(
            $"/api/v1/sensors/{SENSOR_ID}/readings",
            content);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"  [{metrica}] API: {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"  Resposta: {body}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [{metrica}] ERRO API: {ex.Message}");
    }
}

// ============================================================
// HEARTBEAT
// ============================================================

async Task EnviarHeartbeatAsync()
{
    try
    {
        using var response = await http.PostAsync(
            $"/api/v1/sensors/{SENSOR_ID}/heartbeat",
            null);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"[HEARTBEAT] {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[HEARTBEAT] {body}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HEARTBEAT] ERRO: {ex.Message}");
    }
}
