[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/AerisG222/ConvertVideos/blob/master/LICENSE.md)
[![Travis](https://img.shields.io/travis/AerisG222/ConvertVideos.svg)](https://travis-ci.org/AerisG222/ConvertVideos)
[![Coverity Scan](https://img.shields.io/coverity/scan/10075.svg)](https://scan.coverity.com/projects/aerisg222-convertvideos)

# ConvertVideos

A small utility I use to prepare videos of different formats for my website.

# Usage

Below is a sample of how to use via podman:

```bash
podman run -it --rm --security-opt label=disable -v /home/mmorano/Desktop:/src -v /home/mmorano/Desktop:/output localhost/maw-convert-videos-test -c test_category -o /output/vidtest.sql -v /src/convert_videos_test -w /movies -r 'friend admin' -y 2022
```

## Contributing

I'm happy to accept pull requests.  By submitting a pull request, you
must be the original author of code, and must not be breaking
any laws or contracts.

Otherwise, if you have comments, questions, or complaints, please file
issues to this project on the github repo.

## License

MIT

## Reference

- FFMPEG: https://ffmpeg.org/
