;
;	Z86 Basic Input Output System
;	I'll get around to documenting this eventually
;
;	BIOS is loaded at F000:0100
;

format binary
org 0x0100
use16

;
;	EntryPoint
;
EntryPoint:
	;mov		ax, 0x1337
	;mov		cx, 0xBEEF
	;mov		bx, 0x0080
	;mov		si, 0x0080
	;mov		[bx + si + 0x0400], cx
	;mov		dx, [bx + si + 0x0400]
	;mov		[0x0500], bx
	
	mov		bx, 0x1337
	mov		es, bx
	;mov		cs, bx			; Lol don't actually change the CS
	mov		ss, bx
	mov		ds, bx
	
	mov		ax, es
	mov		cx, ss
	mov		dx, ds
	
	mov		ax, 0x1337
	
	hlt
