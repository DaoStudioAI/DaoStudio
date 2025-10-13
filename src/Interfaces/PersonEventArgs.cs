namespace DaoStudio.Interfaces;

/// <summary>
/// Defines the type of person operation performed
/// </summary>
public enum PersonOperationType
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Event arguments for person operation events
/// </summary>
public class PersonOperationEventArgs : EventArgs
{
    public PersonOperationType OperationType { get; set; }
    public IPerson? Person { get; set; }
    public long? PersonId { get; set; }

    /// <summary>
    /// Constructor for Create/Update operations
    /// </summary>
    /// <param name="operationType">The type of operation</param>
    /// <param name="person">The person involved in the operation</param>
    public PersonOperationEventArgs(PersonOperationType operationType, IPerson person)
    {
        OperationType = operationType;
        Person = person;
        PersonId = person.Id;
    }

    /// <summary>
    /// Constructor for Delete operations
    /// </summary>
    /// <param name="personId">The ID of the deleted person</param>
    public PersonOperationEventArgs(long personId)
    {
        OperationType = PersonOperationType.Deleted;
        PersonId = personId;
        Person = null;
    }
}