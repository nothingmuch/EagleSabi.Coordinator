using EagleSabi.Coordinator.Domain.Context.Round;
using EagleSabi.Infrastructure.Common.Abstractions.Common.Modules;
using EagleSabi.Infrastructure.Common.Abstractions.EventSourcing.Modules;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace EagleSabi.Coordinator;

internal record CoordinatorService : IWabiSabiApiRequestHandler
{
    public CoordinatorService(IEventStore eventStore, IUnguessableGuidGenerator guidGenerator, IRPCClient rpcClient)
    {
        EventStore = eventStore;
        GuidGenerator = guidGenerator;
        RpcClient = rpcClient;
    }

    public IEventStore EventStore { get; }

    public IUnguessableGuidGenerator GuidGenerator { get; }
    public IRPCClient RpcClient { get; }

    public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
    {
        // TODO implement something like:
        // var result = await EventStore.ProcessCommandAsync(new GetCoin(request.Outpoint), nameof(ChainMonitorAggregate), "<<singleton>>>");
        // that will succeed if the coin is unspent, and also begin monitoring that outpoint for double spends

        var coin = await OutpointToCoinAsync(request.Input, cancellationToken);

        // TODO check that the coin is not already known and part of some other round and if so
        // throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);

        var aliceSecret = GuidGenerator.NewGuid();

        var command = new RegisterInputCommand(coin, request.OwnershipProof, aliceSecret);
        var result = await EventStore.ProcessCommandAsync(command, nameof(RoundAggregate), request.RoundId.ToString());

        var credentialEvent = result.NewEvents.OfType<CredentialsIssuedEvent>().Single();
        return new InputRegistrationResponse(aliceSecret, credentialEvent.AmountCredentialsResponse, credentialEvent.VsizeCredentialsResponse);
    }

    public async Task<Coin> OutpointToCoinAsync(OutPoint outpoint, CancellationToken cancellationToken)
    {
        var txOutResponse = await RpcClient.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, includeMempool: true, cancellationToken).ConfigureAwait(false);

        if (txOutResponse is null)
        {
            throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputSpent);
        }

        // TODO move to round aggregate?
        // if (txOutResponse.Confirmations == 0)
        // {
        //     throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputUnconfirmed);
        // }
        // if (txOutResponse.IsCoinBase && txOutResponse.Confirmations <= 100)
        // {
        //     throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputImmature);
        // }

        return new Coin(outpoint, txOutResponse.TxOut);
    }

    public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}