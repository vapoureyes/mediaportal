class IMPrtpStream {

public:

	virtual void MPrtpStreamCreate(/*char*, */char*, int, char*) = 0;
	virtual void RtpStop() = 0;
	virtual void play() = 0;
	virtual void write(unsigned char *dataPtr, int numBytes) = 0;
};