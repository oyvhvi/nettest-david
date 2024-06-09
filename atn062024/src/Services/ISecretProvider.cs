using atn062024.Models;

namespace atn062024.Services;

public interface ISecretProvider
{
    String GetSecret(SecretKey secretKey);
}

public class EnvSecretProvider : ISecretProvider
{
    private readonly IConfigurationSection _secretsConfig_;

    public EnvSecretProvider(IConfiguration configuration)
    {
        _secretsConfig_ = configuration.GetSection("Secrets");
    }

    public String GetSecret(SecretKey secretKey) =>
        _secretsConfig_.GetValue<String>(secretKey.ToString())
        ?? throw new InvalidOperationException($"Requested secret [{secretKey}] is null");
}
