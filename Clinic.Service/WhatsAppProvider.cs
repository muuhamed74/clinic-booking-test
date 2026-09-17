using Clinic.Domain.Entities;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Service
{
    public class WhatsAppProvider : IWhatsAppProvider
    {
        private readonly WhatsAppSettings _settings;
        private readonly HttpClient _httpClient;

        public WhatsAppProvider(IOptions<WhatsAppSettings> settings, HttpClient httpClient)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
        }

        public async Task SendAsync(string sessionId, string phoneNumber, string message)
        {
            var payload = new NewApiRequest
            {
                recipients = phoneNumber,
                message = message,
                sessionId = sessionId,
                contentType = "string",
                no_duplication = false
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiToken);

            var response = await _httpClient.PostAsJsonAsync(_settings.ApiUrl, payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"WhatsApp API Error: {error}");
            }
        }


    }
}
