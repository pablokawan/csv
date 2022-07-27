using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Csv.ApiTests.Controllers;

[Route("/")]
public class FileController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PostAsync([Required] IFormFile formFile)
    {
        var fileName = formFile.FileName;
        var isFileNameInvalid = string.IsNullOrWhiteSpace(fileName);
        if (isFileNameInvalid) throw new Exception("Nome de arquivo inválido");

        var fileExtension = Path.GetExtension(fileName);
        var isFileExtensionInvalid = string.IsNullOrWhiteSpace(fileExtension) || !fileExtension.EndsWith(".csv");
        if (isFileExtensionInvalid) throw new Exception("Extensão de arquivo inválida");

        var fileStream = formFile.OpenReadStream();
        var isFileStreamInvalid = fileStream.Length == 0;
        if (isFileStreamInvalid) throw new Exception("Tamanho do arquivo inválido");

        await foreach (var line in CsvReader.ReadFromStreamAsync(fileStream, Encoding.GetEncoding("ISO-8859-1")))
        {
            // Sample csv reading by headers
            var byName = line["ESPÉCIE"];
            var byName1 = line["VALOR"];
            var byName2 = line["SEU NÚMERO"];
            var byName3 = line["VENCIMENTO"];
            var byName4 = line["DATA DESCONTO"];
            var byName5 = line["VALOR DESCONTO"];
            var byName6 = line["CPF/CNPJ"];
            var byName7 = line["SACADO"];
            var byName8 = line["CEP"];
            var byName9 = line["ENDEREÇO"];
            var byName10 = line["BAIRRO"];
            var byName11 = line["CIDADE"];
            var byName12 = line["UF"];
            var byName13 = line["TIPO DE MULTA"];
            var byName14 = line["VALOR MULTA"];
            var byName15 = line["TIPO DE MORA"];
            var byName16 = line["VALOR MORA"];
            var byName17 = line["MENSAGEM 01"];
            var byName18 = line["MENSAGEM 02"];
            var byName19 = line["MENSAGEM 03"];
            var byName20 = line["MENSAGEM 04"];
            var byName21 = line["NUMERO DA CONTA"];
            var byName22 = line["UNIDADE"]; // optional
        }

        return Ok();
    }
}