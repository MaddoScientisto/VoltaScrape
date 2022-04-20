using HtmlAgilityPack;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.IO;
using Spectre.Console;
using RestSharp;
using Newtonsoft.Json;

// See https://aka.ms/new-console-template for more information



var config = await ReadConfigData();
string notionSecret = config.Secret;
string notionDatabase = config.Database;
string url = config.Url;

var response = await GetHtmlPageAsString(url);

var attendees = await ParseHtml(response);

await AddToNotion(attendees);

async Task<Config> ReadConfigData()
{
    var json = await File.ReadAllTextAsync("config.json");
    var config = JsonConvert.DeserializeObject<Config>(json);

    return config;
    
}


async Task<List<Attendee>> ParseHtml(string html)
{
    HtmlDocument doc = new HtmlDocument();
    doc.LoadHtml(html);

    var attendee_fw = doc.DocumentNode.Descendants("div")
        .Where(x => x.HasClass("attendee_fw")).FirstOrDefault();

    var attendeesNodes = attendee_fw.Descendants("article").FirstOrDefault().Descendants("section");

    var attendees = new List<Attendee>();

    foreach (var attendeeNode in attendeesNodes)
    {
        try
        {



            var name = attendeeNode.DescendantNodes().FirstOrDefault(x => x.Name == "h6").InnerText;

            var img = attendeeNode.DescendantNodes().FirstOrDefault(x => x.Name == "img").Attributes["src"].Value;

            var userUrl = attendeeNode.DescendantNodes().FirstOrDefault(x => x.Name == "div" && x.HasClass("attendee_name")).Descendants("a").FirstOrDefault().Attributes["href"].Value;

            var userPage = await GetHtmlPageAsString(userUrl);

            var userDoc = new HtmlDocument();
            userDoc.LoadHtml(userPage);


            var infoNode = userDoc.DocumentNode.Descendants("div").FirstOrDefault(x => x.HasClass("attendee_info"));
            var nickName = infoNode.Descendants("h3").FirstOrDefault(x => x.HasClass("name_bold"));
            //var nameNode = nickName.NextSibling;
            var typeNode = infoNode.Descendants("h4").FirstOrDefault(x => x.HasClass("info_small25")).InnerText;

            var typeClass = HtmlEntity.DeEntitize(typeNode);
            var country = typeClass.Split('(', ')');

            var aboutAuthorNode = userDoc.DocumentNode.Descendants().FirstOrDefault(x => x.Attributes["id"]?.Value == "about_the_author");
            var gravatar = aboutAuthorNode.Descendants("img").FirstOrDefault(x => x.HasClass("gravatar")).Attributes["src"].Value;

            var nam = aboutAuthorNode.Descendants().FirstOrDefault(x => x.HasClass("author_name")).InnerText;

            var attendeeObject = new Attendee()
            {
                FullName = HtmlEntity.DeEntitize(name).Trim(),
                Image = img.Trim(),
                NickName = HtmlEntity.DeEntitize(nickName.InnerText).Trim(),
                //Name = HtmlEntity.DeEntitize(nameNode.InnerText),
                Type = HtmlEntity.DeEntitize(country[0]).Trim(),
                Country = HtmlEntity.DeEntitize(country[1]).Trim(),
                Gravatar = gravatar.Trim(),
                Name = HtmlEntity.DeEntitize(nam).Trim()
            };

            attendees.Add(attendeeObject);
            AnsiConsole.WriteLine($"Added: {attendeeObject.FullName}");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            throw;
        }
    }

    return attendees;
}

async Task<string> GetHtmlPageAsString(string url)
{
    try
    {

        HttpClient client = new HttpClient();
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
        client.DefaultRequestHeaders.Accept.Clear();
        var response = await client.GetStringAsync(url);
        return response;
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex);
        throw;
    }

}


async Task AddToNotion(List<Attendee> attendees)
{
    string url = "https://api.notion.com/v1/pages";
    

    foreach(var attendee in attendees)
    {
        RestClient client = new RestClient(url);
        RestRequest request = new RestRequest(url);
        request.AddHeader("authorization", $"Bearer {notionSecret}");
        request.AddHeader("Content-Type", "application/json; charset=utf-8");
        request.AddHeader("Notion-Version", "2022-02-22");
        request.RequestFormat = DataFormat.Json;

        var data = new
        {
            parent = new
            {
                database_id = notionDatabase
            },
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[]
                    {
                        new
                        {
                            Type = "text",
                            Text = new
                            {
                                content = attendee.FullName
                            }
                        }
                    }
                },
                NickName = new
                {
                    rich_text = new[]
                    {
                        new
                        {
                            type = "text",
                            text = new
                            {
                                content = attendee.NickName
                            }
                        }
                    }
                },
                Name = new
                {
                    rich_text = new[]
                    {
                        new
                        {
                            type = "text",
                            text = new
                            {
                                content = attendee.Name
                            }
                        }
                    }
                },
                country = new
                {
                    select = new
                    {
                        name = attendee.Country
                    }
                },
                type = new
                {
                    select = new
                    {
                        name = attendee.Type
                    }
                },
                image = new
                {                    
                        url = attendee.Image                    
                },
                gravatar = new
                {                    
                        url = attendee.Gravatar                    
                },
                avatar = new
                {
                    files = new []
                    {
                        new
                        {
                            type = "external",
                            name = $"{attendee.NickName}.jpg",
                            external = new
                            {
                                url = attendee.Gravatar
                            }
                        }
                    }
                }
            }
        };
        request.AddJsonBody(data);
        try
        {
            var res = await client.ExecutePostAsync(request);
            AnsiConsole.WriteLine($"Uploaded {attendee.NickName}");
            await Task.Delay(400);
            
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            throw;
        }
        

    }

}
class Attendee
{
    public string NickName { get; set; }
    public string FullName { get; set; }
    public string Image { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Country { get; set; }

    public string Gravatar { get; set; }
}

class Config
{
    public string Secret { get; set; }
    public string Database { get; set; }
    public string Url { get; set; }
}