using Amazon.S3;
using Amazon.S3.Model;
using Server.Models;

namespace Server.Operators
{
    public class BucketOperator
    { 
        public static async Task<IEnumerable<S3ObjectDtoModel>> GetObjectsFromBucket(
            string key, IAmazonS3 s3Client, IConfiguration configuration)
        {
            string bucketName = configuration["AWS:BucketName"];
            _ = long.TryParse(configuration["AWS:LinkValidityInSeconds"], out long linkValidityInSeconds);

            if (!await s3Client.DoesS3BucketExistAsync(bucketName))
                throw new Exception("Bucket doesn't exist");

            var request = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = key
            };
            var result = await s3Client.ListObjectsV2Async(request);

            IEnumerable<S3ObjectDtoModel> s3Object;
            try
            {
                s3Object = result.S3Objects.Select(s =>
                {
                    var urlRequest = new GetPreSignedUrlRequest()
                    {
                        BucketName = bucketName,
                        Key = s.Key,
                        Expires = DateTime.UtcNow.AddSeconds(linkValidityInSeconds)
                    };
                    return new S3ObjectDtoModel()
                    {
                        Name = s.Key.ToString(),
                        PresignedUrl = s3Client.GetPreSignedURL(urlRequest),
                    };
                });
            }
            catch
            { throw new Exception("Picture in bucket has unreadable name"); }
            return s3Object;
        }

        public static async Task<bool> UpdateFileInBucket(
            string key, IFormFile file, IAmazonS3 s3Client, IConfiguration configuration)
        {
            string bucketName = configuration["AWS:BucketName"];

            if (!await s3Client.DoesS3BucketExistAsync(bucketName))
                throw new Exception("S3 bucket does not exist");

            var request = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = key,
                InputStream = file.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", file.ContentType);
            await s3Client.PutObjectAsync(request);

            return true;
        }
    }
}
