using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Azure.AI.Language.QuestionAnswering;

class Program
{
    private static string translatorEndpoint = "https://api.cognitive.microsofttranslator.com";
    private static string cogSvcKey;
    private static readonly string cogSvcRegion = "northeurope";
    private static string cogSvcEndpoint;

    private static string botSvcKey;
    private static string botEndpoint;
    private static string deploymentName = "production";
    private static string projectName = "ResturangQnA";

    private static bool exit = false;
    static void Main(string[] args)
    {
        try
        {
            // Get config settings from AppSettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
            cogSvcKey = configuration["CognitiveServiceKey"];

            // Get confiq settings from Appsettings for Bot
            botEndpoint = configuration["BotEndpoint"];
            botSvcKey = configuration["BotKey"];
            

            // Set console encoding to unicode
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        try
        {
            Console.WriteLine("Please write a question to the bot about our restaurang or EXIT to exit the program:");
            do
            {

                string userInput = Console.ReadLine();
                if (userInput.ToLower() == "exit")
                {
                    exit = true;
                    break;
                }
                else
                {
                    Translation(userInput);
                }

            } while (exit == false);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    static async void Translation(string question)
    {
        try
        {

            // Create client using endpoint and key for the translation
            AzureKeyCredential credentials = new AzureKeyCredential(cogSvcKey);
            Uri endpoint = new Uri(cogSvcEndpoint);
            TextAnalyticsClient CogClient = new TextAnalyticsClient(endpoint, credentials);

            // Get language
            DetectedLanguage detectedLanguage = CogClient.DetectLanguage(question);
            var language = detectedLanguage.Iso6391Name;

            // Translate if not already English
            Console.Clear();
            if (language != "en")
            {
                string translatedText = await Translate(question, language);
                Console.WriteLine("You: " + translatedText);
                BotAnswer(translatedText);
            }
            if(language == "en")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("You: " + question);
                Console.ForegroundColor = ConsoleColor.Green;
                BotAnswer(question);
            }
           
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
   
    static async Task<string> Translate(string text, string sourceLanguage)
    {
        string translation = "";

        // Use the Translator translate function
        object[] body = new object[] { new { Text = text } };
        var requestBody = JsonConvert.SerializeObject(body);
        using (var client = new HttpClient())
        {
            using (var request = new HttpRequestMessage())
            {
                // Build the request
                string path = "/translate?api-version=3.0&from=" + sourceLanguage + "&to=en";
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(translatorEndpoint + path);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", cogSvcKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", cogSvcRegion);

                // Send the request and get response
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                // Read response as a string
                string responseContent = await response.Content.ReadAsStringAsync();

                // Parse JSON array and get translation
                JArray jsonResponse = JArray.Parse(responseContent);
                translation = (string)jsonResponse[0]["translations"][0]["text"];
            }
        }


        // Return the translation
        return translation;

    }
    static async void BotAnswer(string question)
    {
        // Create client using endpoint and key for the bot
        AzureKeyCredential credentialbot = new AzureKeyCredential(botSvcKey);
        Uri botEndpointUri = new Uri(botEndpoint);

        QuestionAnsweringClient client = new QuestionAnsweringClient(botEndpointUri, credentialbot);
        QuestionAnsweringProject project = new QuestionAnsweringProject(projectName, deploymentName);

        Response<AnswersResult> respone = client.GetAnswers(question, project);

        foreach (KnowledgeBaseAnswer answer in respone.Value.Answers)
        {
            Console.WriteLine($"\nBot:{answer.Answer}");
        }
    }
    
}