package com.example.infoapp.proto

import com.example.infoapp.proto.DataTransferServiceGrpc.getServiceDescriptor
import io.grpc.CallOptions
import io.grpc.CallOptions.DEFAULT
import io.grpc.Channel
import io.grpc.Metadata
import io.grpc.MethodDescriptor
import io.grpc.ServerServiceDefinition
import io.grpc.ServerServiceDefinition.builder
import io.grpc.ServiceDescriptor
import io.grpc.Status.UNIMPLEMENTED
import io.grpc.StatusException
import io.grpc.kotlin.AbstractCoroutineServerImpl
import io.grpc.kotlin.AbstractCoroutineStub
import io.grpc.kotlin.ClientCalls.unaryRpc
import io.grpc.kotlin.ServerCalls.unaryServerMethodDefinition
import io.grpc.kotlin.StubFor
import kotlin.String
import kotlin.coroutines.CoroutineContext
import kotlin.coroutines.EmptyCoroutineContext
import kotlin.jvm.JvmOverloads
import kotlin.jvm.JvmStatic

/**
 * Holder for Kotlin coroutine-based client and server APIs for
 * com.example.infoapp.DataTransferService.
 */
public object DataTransferServiceGrpcKt {
  public const val SERVICE_NAME: String = DataTransferServiceGrpc.SERVICE_NAME

  @JvmStatic
  public val serviceDescriptor: ServiceDescriptor
    get() = getServiceDescriptor()

  public val sendContactsMethod: MethodDescriptor<ContactList, TransferResponse>
    @JvmStatic
    get() = DataTransferServiceGrpc.getSendContactsMethod()

  public val sendCallLogsMethod: MethodDescriptor<CallLogList, TransferResponse>
    @JvmStatic
    get() = DataTransferServiceGrpc.getSendCallLogsMethod()

  /**
   * A stub for issuing RPCs to a(n) com.example.infoapp.DataTransferService service as suspending
   * coroutines.
   */
  @StubFor(DataTransferServiceGrpc::class)
  public class DataTransferServiceCoroutineStub @JvmOverloads constructor(
    channel: Channel,
    callOptions: CallOptions = DEFAULT,
  ) : AbstractCoroutineStub<DataTransferServiceCoroutineStub>(channel, callOptions) {
    override fun build(channel: Channel, callOptions: CallOptions): DataTransferServiceCoroutineStub
        = DataTransferServiceCoroutineStub(channel, callOptions)

    /**
     * Executes this RPC and returns the response message, suspending until the RPC completes
     * with [`Status.OK`][io.grpc.Status].  If the RPC completes with another status, a
     * corresponding
     * [StatusException] is thrown.  If this coroutine is cancelled, the RPC is also cancelled
     * with the corresponding exception as a cause.
     *
     * @param request The request message to send to the server.
     *
     * @param headers Metadata to attach to the request.  Most users will not need this.
     *
     * @return The single response from the server.
     */
    public suspend fun sendContacts(request: ContactList, headers: Metadata = Metadata()):
        TransferResponse = unaryRpc(
      channel,
      DataTransferServiceGrpc.getSendContactsMethod(),
      request,
      callOptions,
      headers
    )

    /**
     * Executes this RPC and returns the response message, suspending until the RPC completes
     * with [`Status.OK`][io.grpc.Status].  If the RPC completes with another status, a
     * corresponding
     * [StatusException] is thrown.  If this coroutine is cancelled, the RPC is also cancelled
     * with the corresponding exception as a cause.
     *
     * @param request The request message to send to the server.
     *
     * @param headers Metadata to attach to the request.  Most users will not need this.
     *
     * @return The single response from the server.
     */
    public suspend fun sendCallLogs(request: CallLogList, headers: Metadata = Metadata()):
        TransferResponse = unaryRpc(
      channel,
      DataTransferServiceGrpc.getSendCallLogsMethod(),
      request,
      callOptions,
      headers
    )
  }

  /**
   * Skeletal implementation of the com.example.infoapp.DataTransferService service based on Kotlin
   * coroutines.
   */
  public abstract class DataTransferServiceCoroutineImplBase(
    coroutineContext: CoroutineContext = EmptyCoroutineContext,
  ) : AbstractCoroutineServerImpl(coroutineContext) {
    /**
     * Returns the response to an RPC for com.example.infoapp.DataTransferService.SendContacts.
     *
     * If this method fails with a [StatusException], the RPC will fail with the corresponding
     * [io.grpc.Status].  If this method fails with a [java.util.concurrent.CancellationException],
     * the RPC will fail
     * with status `Status.CANCELLED`.  If this method fails for any other reason, the RPC will
     * fail with `Status.UNKNOWN` with the exception as a cause.
     *
     * @param request The request from the client.
     */
    public open suspend fun sendContacts(request: ContactList): TransferResponse = throw
        StatusException(UNIMPLEMENTED.withDescription("Method com.example.infoapp.DataTransferService.SendContacts is unimplemented"))

    /**
     * Returns the response to an RPC for com.example.infoapp.DataTransferService.SendCallLogs.
     *
     * If this method fails with a [StatusException], the RPC will fail with the corresponding
     * [io.grpc.Status].  If this method fails with a [java.util.concurrent.CancellationException],
     * the RPC will fail
     * with status `Status.CANCELLED`.  If this method fails for any other reason, the RPC will
     * fail with `Status.UNKNOWN` with the exception as a cause.
     *
     * @param request The request from the client.
     */
    public open suspend fun sendCallLogs(request: CallLogList): TransferResponse = throw
        StatusException(UNIMPLEMENTED.withDescription("Method com.example.infoapp.DataTransferService.SendCallLogs is unimplemented"))

    final override fun bindService(): ServerServiceDefinition = builder(getServiceDescriptor())
      .addMethod(unaryServerMethodDefinition(
      context = this.context,
      descriptor = DataTransferServiceGrpc.getSendContactsMethod(),
      implementation = ::sendContacts
    ))
      .addMethod(unaryServerMethodDefinition(
      context = this.context,
      descriptor = DataTransferServiceGrpc.getSendCallLogsMethod(),
      implementation = ::sendCallLogs
    )).build()
  }
}
