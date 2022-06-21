using System;
using System.Threading.Tasks;
using IntegrationTests;
using Jasper.Persistence.Marten;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestingSupport;
using TestingSupport.Sagas;
using Weasel.Core;

namespace Jasper.Persistence.Testing.Marten.Persistence.Sagas;

public class MartenSagaHost : ISagaHost
{
    private IHost _host;

    public IHost BuildHost<TSaga>()
    {
        _host = JasperHost.For(opts =>
        {
            opts.Handlers.DisableConventionalDiscovery().IncludeType<TSaga>();

            opts.Services.AddMarten(x =>
            {
                x.Connection(Servers.PostgresConnectionString);
                x.DatabaseSchemaName = "sagas";
                x.AutoCreateSchemaObjects = AutoCreate.All;
            }).IntegrateWithJasper();

            opts.PublishAllMessages().Locally();
        });

        return _host;
    }

    public Task<T> LoadState<T>(Guid id) where T : class
    {
        return _host.Services.GetRequiredService<IQuerySession>().LoadAsync<T>(id);
    }

    public Task<T> LoadState<T>(int id) where T : class
    {
        return _host.Services.GetRequiredService<IQuerySession>().LoadAsync<T>(id);
    }

    public Task<T> LoadState<T>(long id) where T : class
    {
        return _host.Services.GetRequiredService<IQuerySession>().LoadAsync<T>(id);
    }

    public Task<T> LoadState<T>(string id) where T : class
    {
        return _host.Services.GetRequiredService<IQuerySession>().LoadAsync<T>(id);
    }
}
