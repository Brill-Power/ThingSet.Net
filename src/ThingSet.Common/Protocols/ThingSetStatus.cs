/*
 * Copyright (c) 2023-2025 Brill Power.
 *
 * SPDX-License-Identifier: Apache-2.0
 */
namespace ThingSet.Common.Protocols;

public enum ThingSetStatus : byte
{
    Created = 0x81, /** Response code for successful CREATE requests. */
	Deleted = 0x82, /** Response code for successful DELETE requests. */
	Changed = 0x84, /** Response code for successful EXEC/UPDATE requests. */
	Content = 0x85, /** Response code for successful GET/FETCH requests. */

    /* Status codes (client errors) */
	BadRequest         = 0xA0, /** Error code: Bad request. */
	Unauthorised       = 0xA1, /** Error code: Authentication needed. */
	Forbidden          = 0xA3, /** Error code: Access forbidden. */
	NotFound           = 0xA4, /** Error code: Data object not found. */
	MethodNotAllowed   = 0xA5, /** Error code: Method not allowed. */
	RequestIncomplete  = 0xA8, /** Error code: Request incomplete. */
	Conflict           = 0xA9, /** Error code: Conflict. */
	RequestTooLarge    = 0xAD, /** Error code: Request not fitting into buffer. */
	UnsupportedFormat  = 0xAF, /** Error code: Format for an item not supported. */

    /* Status codes (server errors) */
	InternalServerError = 0xC0, /** Error code: Generic catch-all response. */
	NotImplemented      = 0xC1, /** Error code: Request method not implemented. */
	GatewayTimeout      = 0xC4, /** Error code: Node cannot be reached. */
	NotAGateway         = 0xC5, /** Error code: Node is not a gateway. */
	ResponseTooLarge	= 0xE1, /** Error code: Response not fitting into buffer. */
}