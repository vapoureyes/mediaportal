/* 
 *	Copyright (C) 2006-2008 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#include <windows.h>




class CHuffmanBranchNode
	: public CHuffmanNode
{
public:

	//	Cstr/Flzr
	CHuffmanBranchNode();
	~CHuffmanBranchNode();

	//	Sets the left node
	void SetLeft(CHuffmanNode* newLeft);

	//	Sets the right node
	void SetRight(CHuffmanNode* newRight);

	//	Gets the left node
	CHuffmanNode* GetLeft();

	//	Gets the right node
	CHuffmanNode* GetRight();

	//	Gets the type of the node
	int GetType();

	//	Traverses the tree to decode the huffman data
	bool Decode(byte** currentInputByte, byte* currentInputBit, byte* endInputByte, byte** currentOutputByte, byte* endOutputByte, bool* hasFinished);

private:
	
	//	Left/Right tree pointer (left=0, right=1)
	CHuffmanNode* left;
	CHuffmanNode* right;

};