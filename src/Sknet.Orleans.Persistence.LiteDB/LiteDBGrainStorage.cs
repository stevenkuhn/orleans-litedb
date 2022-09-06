namespace Sknet.Orleans.Persistence.LiteDB;

public class LiteDBGrainStorage : IGrainStorage
{
    private readonly ILiteDatabase _database;

    public LiteDBGrainStorage(
        ILiteDatabase database,
        ILogger<LiteDBGrainStorage> logger)
    {
        _database = database;
    }

    public Task ClearStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
    {
        var documentId = GetDocumentId(grainReference);
        var collectionName = _database.Mapper.ResolveCollectionName(grainState.Type);

        var collection = _database.GetCollection(collectionName);

        collection.Delete(documentId);

        return Task.CompletedTask;
    }

    public Task ReadStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
    {
        var documentId = GetDocumentId(grainReference);
        var collectionName = _database.Mapper.ResolveCollectionName(grainState.Type);

        var collection = _database.GetCollection(collectionName);
        var document = collection.FindById(documentId);

        if (document == null)
        {
            grainState.ETag = null;
            grainState.RecordExists = false;
            grainState.State = null;

            return Task.CompletedTask;
        }

        var state = _database.Mapper.ToObject(grainState.Type, document);

        grainState.ETag = null;
        grainState.RecordExists = true;
        grainState.State = state;

        return Task.CompletedTask;
    }

    public Task WriteStateAsync(string grainType, GrainReference grainReference, IGrainState grainState)
    {
        var documentId = GetDocumentId(grainReference);
        var collectionName = _database.Mapper.ResolveCollectionName(grainState.Type);

        var collection = _database.GetCollection(collectionName);
        var document = _database.Mapper.ToDocument(grainState.Type, grainState.State);

        collection.Upsert(documentId, document);

        grainState.ETag = null;
        grainState.RecordExists = true;

        return Task.CompletedTask;
    }

    private BsonValue GetDocumentId(GrainReference grainReference)
    {
        if (grainReference.IsPrimaryKeyBasedOnLong())
        {
            return new BsonValue(grainReference.GetPrimaryKeyLong());
        }

        var guidId = grainReference.GetPrimaryKey();
        if (guidId != Guid.Empty)
        {
            return new BsonValue(grainReference.GetPrimaryKey());
        }

        return new BsonValue(grainReference.GetPrimaryKeyString());
    }
}
