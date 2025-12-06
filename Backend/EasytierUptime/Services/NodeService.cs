using EasytierUptime.Data;
using EasytierUptime_Entities.Entities;

namespace EasytierUptime.Services;

public class NodeService
{
    public async Task<SharedNode> UpsertAsync(SharedNode input)
    {
        if (input.Id > 0)
        {
            var existing = await FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == input.Id).FirstAsync();
            if (existing is null) throw new InvalidOperationException("node not found");

            // update fields
            existing.Name = input.Name;
            existing.Host = input.Host;
            existing.Port = input.Port;
            existing.Protocol = input.Protocol;
            existing.AllowRelay = input.AllowRelay;
            existing.NetworkName = input.NetworkName;
            existing.NetworkSecret = input.NetworkSecret;
            existing.Description = input.Description;
            existing.MaxConnections = input.MaxConnections;
            existing.QqNumber = input.QqNumber;
            existing.Wechat = input.Wechat;
            existing.Mail = input.Mail;
            existing.IsApproved = input.IsApproved;
            existing.IsActive = input.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await FreeSqlDb.Orm.Update<SharedNode>().SetSource(existing).ExecuteAffrowsAsync();
            return existing;
        }
        input.CreatedAt = DateTime.UtcNow;
        input.UpdatedAt = DateTime.UtcNow;
        await FreeSqlDb.Orm.Insert(input).ExecuteAffrowsAsync();
        return input;
    }

    public Task<List<SharedNode>> ListAsync(bool? approved = null)
    {
        var sel = FreeSqlDb.Orm.Select<SharedNode>();
        if (approved.HasValue) sel = sel.Where(x => x.IsApproved == approved.Value);
        return sel.OrderByDescending(x => x.UpdatedAt).ToListAsync();
    }

    public Task<SharedNode?> GetAsync(int id) => FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.Id == id).FirstAsync();
}
