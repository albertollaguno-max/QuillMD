namespace PlannamTypora.Services
{
    public static class HtmlToMarkdown
    {
        public static string Convert(string html)
        {
            var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
            {
                GithubFlavored = true,
                RemoveComments = true,
                SmartHrefHandling = true
            });
            return converter.Convert(html);
        }
    }
}
