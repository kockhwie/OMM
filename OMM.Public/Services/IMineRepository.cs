using OMM.Public.Data.Entities;

namespace OMM.Public.Services;

public interface IMineRepository
{
    Task<IReadOnlyList<MineEntity>> GetMinesAsync(CancellationToken cancellationToken = default);

    Task<MineEntity?> GetMineAsync(Guid mineId, CancellationToken cancellationToken = default);

    Task<MineEntity> AddMineAsync(MineEntity mine, CancellationToken cancellationToken = default);

    Task<MineEntity?> UpdateMineAsync(MineEntity mine, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteMineAsync(Guid mineId, CancellationToken cancellationToken = default);

    Task<MinePositionEntity?> GetPositionAsync(Guid mineId, Guid positionId, CancellationToken cancellationToken = default);

    Task<MinePositionEntity> AddPositionAsync(MinePositionEntity position, CancellationToken cancellationToken = default);

    Task<MinePositionEntity?> UpdatePositionAsync(MinePositionEntity position, CancellationToken cancellationToken = default);

    Task<bool> SoftDeletePositionAsync(Guid mineId, Guid positionId, CancellationToken cancellationToken = default);
}
